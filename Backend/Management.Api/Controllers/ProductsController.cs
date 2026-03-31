using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Management.Infrastructure.Repositories;
using Management.Domain.Entities;
using System.Text.Json;

namespace Management.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly IAffiliateClickRepository _affiliateClickRepository;
        private readonly IProductRepository _productRepository;
        private readonly ProductAffiliateLinkRepository _productAffiliateLinks;
        private readonly IConfiguration _config;

        private static decimal? ToVnd(long? raw, string? platform)
        {
            if (raw == null) return null;
            var r = raw.Value;
            decimal vnd;
            // Shopee prices are commonly returned in 1e-5 units (price * 100000).
            if (string.Equals(platform, "Shopee", StringComparison.OrdinalIgnoreCase))
            {
                vnd = r / 100000m;
            }
            // AccessTrade is persisted as VND * 1000 in our ingestion code.
            else if (string.Equals(platform, "AccessTrade", StringComparison.OrdinalIgnoreCase))
            {
                vnd = r / 1000m;
            }
            else if (r % 1000L == 0L)
            {
                vnd = r / 1000m;
            }
            else
            {
                // Some historical data may already be persisted in VND (no scaling).
                vnd = r;
            }
            return decimal.Round(vnd, 0, MidpointRounding.AwayFromZero);
        }

        private static float? ExtractRatingStar(string? itemRatingJson)
        {
            if (string.IsNullOrWhiteSpace(itemRatingJson)) return null;
            try
            {
                using var doc = JsonDocument.Parse(itemRatingJson);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
                if (!doc.RootElement.TryGetProperty("rating_star", out var el) || el.ValueKind == JsonValueKind.Null) return null;
                if (el.TryGetSingle(out var s)) return s;
                if (el.TryGetDouble(out var d)) return (float)d;
                return null;
            }
            catch
            {
                return null;
            }
        }

        private static float? ParseCommissionRate(string? defaultCommissionRateRaw)
        {
            if (string.IsNullOrWhiteSpace(defaultCommissionRateRaw)) return null;
            var s = defaultCommissionRateRaw.Trim().Replace("%", "").Replace(",", ".");
            if (!float.TryParse(s, out var pct)) return null;
            return pct / 100f;
        }

        public ProductsController(
            IAffiliateClickRepository affiliateClickRepository,
            IProductRepository productRepository,
            ProductAffiliateLinkRepository productAffiliateLinks,
            IConfiguration config)
        {
            _affiliateClickRepository = affiliateClickRepository;
            _productRepository = productRepository;
            _productAffiliateLinks = productAffiliateLinks;
            _config = config;
        }

        [HttpGet("db")]
        public async Task<IActionResult> GetFromDb([FromQuery] string? search, [FromQuery] int limit = 50)
        {
            var list = await _productRepository.GetLatestAsync(search, limit, null);
            var imagePrefix = GetShopeeCdnImagePrefix();

            var result = list.Select(p => new
            {
                id = p.Id,
                platform = p.Platform,
                externalItemId = p.ExternalItemId,
                shopeeItemId = p.ShopeeItemId,
                name = p.Name,
                slug = p.Slug,
                imageUrl = BuildCdnUrl(imagePrefix, p.ImageUrl),
                price = ToVnd(p.PriceRaw, p.Platform),
                originalPrice = ToVnd(p.PriceBeforeDiscountRaw, p.Platform),
                rating = ExtractRatingStar(p.ItemRatingJson),
                sold = p.SoldRaw,
                commissionRate = ParseCommissionRate(p.DefaultCommissionRateRaw),
            });

            return Ok(result);
        }

        [HttpGet("db/{id:long}")]
        public async Task<IActionResult> GetDbDetail([FromRoute] long id)
        {
            var p = await _productRepository.GetByIdAsync(id);
            if (p == null) return NotFound();

            var imagePrefix = GetShopeeCdnImagePrefix();
            var videoPrefix = GetShopeeCdnVideoPrefix();

            var images = ParseStringArrayJson(p.ImagesJson);

            var imageUrls = images
                .Select(x => BuildCdnUrl(imagePrefix, x))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();

            var videoUrls = ExtractVideoUrls(p.VideoInfoListJson, videoPrefix);

            return Ok(new
            {
                id = p.Id,
                platform = p.Platform,
                externalItemId = p.ExternalItemId,
                shopeeItemId = p.ShopeeItemId,
                shopId = p.ShopId,

                name = p.Name,
                description = p.Description,
                slug = p.Slug,
                imageKey = p.ImageUrl,
                imageUrl = BuildCdnUrl(imagePrefix, p.ImageUrl),
                imageUrls,

                productLink = p.ProductLink,

                maxCommissionRateRaw = p.MaxCommissionRateRaw,
                sellerCommissionRateRaw = p.SellerCommissionRateRaw,
                defaultCommissionRateRaw = p.DefaultCommissionRateRaw,
                commissionRate = ParseCommissionRate(p.DefaultCommissionRateRaw),

                price = ToVnd(p.PriceRaw, p.Platform),
                originalPrice = ToVnd(p.PriceBeforeDiscountRaw, p.Platform),
                rating = ExtractRatingStar(p.ItemRatingJson),
                sold = p.SoldRaw,

                tierVariationsJson = p.TierVariationsJson,
                itemRatingJson = p.ItemRatingJson,
                videoInfoListJson = p.VideoInfoListJson,
                videoUrls,

                createdAt = p.CreatedAt,
                updatedAt = p.UpdatedAt,
                lastSynced = p.LastSynced,
            });
        }

        [HttpPost("buy-now")]
        [Authorize]
        public async Task<IActionResult> BuyNow([FromBody] BuyNowRequest request)
        {
            var userId = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            if (string.IsNullOrWhiteSpace(request.Platform))
            {
                return BadRequest("Platform is required");
            }

            if (string.IsNullOrWhiteSpace(request.ExternalItemId))
            {
                return BadRequest("ExternalItemId is required");
            }

            if (string.IsNullOrWhiteSpace(request.ShopeeItemId))
            {
                return BadRequest("ShopeeItemId is required");
            }

            var product = await _productRepository.UpsertByExternalAsync(new Product
            {
                Platform = request.Platform,
                ExternalItemId = request.ExternalItemId,
                ShopeeItemId = request.ShopeeItemId,
                Name = request.Name ?? string.Empty,
                Slug = string.IsNullOrWhiteSpace(request.Slug)
                    ? BuildProductSlug(request.Name, request.ExternalItemId)
                    : request.Slug,
                ImageUrl = request.ImageUrl,
                LastSynced = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            });

            var click = new AffiliateClick
            {
                UserId = Guid.Parse(userId),
                ProductId = product.Id,
                ClickTime = DateTime.UtcNow,
                IPAddress = request.IPAddress,
                UserAgent = request.UserAgent,
                IsProcessed = false
            };

            var trackedClick = await _affiliateClickRepository.CreateAsync(click);

            // Generate affiliate link (this would be platform-specific)
            var affiliateLink = GenerateAffiliateLink(product, trackedClick.Id);

            return Ok(new { ClickId = trackedClick.Id, AffiliateLink = affiliateLink });
        }

        [HttpPost("import")]
        public async Task<IActionResult> ImportProducts([FromBody] ImportRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Data))
                {
                    return BadRequest("Data is required");
                }

                JsonDocument doc;
                try
                {
                    doc = JsonDocument.Parse(request.Data);
                }
                catch (JsonException ex)
                {
                    return BadRequest($"Invalid JSON data: {ex.Message}");
                }

                JsonElement itemsRoot;
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    itemsRoot = doc.RootElement;
                }
                else if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("data", out var dataEl)
                    && dataEl.ValueKind == JsonValueKind.Object
                    && dataEl.TryGetProperty("list", out var listEl)
                    && listEl.ValueKind == JsonValueKind.Array)
                {
                    itemsRoot = listEl;
                }
                else
                {
                    return BadRequest("Data must be a JSON array or a wrapper object with data.list array");
                }

                var importItems = itemsRoot.EnumerateArray().ToList();
                if (importItems.Count == 0)
                {
                    return BadRequest("No items found in data");
                }

                int successCount = 0;
                int errorCount = 0;
                var errors = new List<string>();

                foreach (var item in importItems)
                {
                    try
                    {
                        if (item.ValueKind != JsonValueKind.Object)
                        {
                            errors.Add("Item ?: Invalid item format");
                            errorCount++;
                            continue;
                        }

                        static string GetStringOrNumberAsString(JsonElement el)
                        {
                            if (el.ValueKind == JsonValueKind.String) return el.GetString() ?? string.Empty;
                            if (el.ValueKind == JsonValueKind.Number)
                            {
                                if (el.TryGetInt64(out var n)) return n.ToString();
                            }
                            return string.Empty;
                        }

                        var itemId = item.TryGetProperty("item_id", out var itemIdEl) ? GetStringOrNumberAsString(itemIdEl) : string.Empty;
                        if (string.IsNullOrWhiteSpace(itemId))
                        {
                            itemId = item.TryGetProperty("itemid", out var itemId2El) ? GetStringOrNumberAsString(itemId2El) : string.Empty;
                        }

                        var productLink = item.TryGetProperty("product_link", out var productLinkEl) ? (productLinkEl.GetString() ?? string.Empty) : string.Empty;

                        var maxCommissionRateRaw = item.TryGetProperty("max_commission_rate", out var mcrEl) ? (mcrEl.GetString() ?? string.Empty) : string.Empty;
                        var sellerCommissionRateRaw = item.TryGetProperty("seller_commission_rate", out var scrEl) ? (scrEl.GetString() ?? string.Empty) : string.Empty;
                        var defaultCommissionRateRaw = item.TryGetProperty("default_commission_rate", out var dcrEl) ? (dcrEl.GetString() ?? string.Empty) : string.Empty;

                        var hasLegacyPayload = item.TryGetProperty("batch_item_for_item_card_full", out var legacyProductData)
                            && legacyProductData.ValueKind == JsonValueKind.Object;

                        var productData = hasLegacyPayload ? legacyProductData : item;

                        var name = productData.TryGetProperty("name", out var nameEl) ? (nameEl.GetString() ?? string.Empty) : string.Empty;
                        var image = productData.TryGetProperty("image", out var imageEl) ? (imageEl.GetString() ?? string.Empty) : string.Empty;

                        string? shopId = null;
                        if (productData.TryGetProperty("shopid", out var shopIdEl) && shopIdEl.ValueKind != JsonValueKind.Null)
                        {
                            if (shopIdEl.ValueKind == JsonValueKind.Number)
                            {
                                if (shopIdEl.TryGetInt64(out var sid)) shopId = sid.ToString();
                            }
                            else if (shopIdEl.ValueKind == JsonValueKind.String)
                            {
                                var s = shopIdEl.GetString();
                                if (!string.IsNullOrWhiteSpace(s)) shopId = s;
                            }
                        }

                        string? imagesJson = null;
                        if (productData.TryGetProperty("images", out var imagesEl) && imagesEl.ValueKind != JsonValueKind.Null)
                        {
                            imagesJson = imagesEl.GetRawText();
                        }

                        string? videoInfoListJson = null;
                        if (productData.TryGetProperty("video_info_list", out var videoInfoEl) && videoInfoEl.ValueKind != JsonValueKind.Null)
                        {
                            videoInfoListJson = videoInfoEl.GetRawText();
                        }

                        string? tierVariationsJson = null;
                        if (productData.TryGetProperty("tier_variations", out var tierEl) && tierEl.ValueKind != JsonValueKind.Null)
                        {
                            tierVariationsJson = tierEl.GetRawText();
                        }

                        string? itemRatingJson = null;
                        if (productData.TryGetProperty("item_rating", out var itemRatingEl) && itemRatingEl.ValueKind != JsonValueKind.Null)
                        {
                            itemRatingJson = itemRatingEl.GetRawText();
                        }

                        int? offerCardType = null;
                        if (productData.TryGetProperty("offer_card_type", out var offerCardTypeEl) && offerCardTypeEl.ValueKind != JsonValueKind.Null)
                        {
                            if (offerCardTypeEl.ValueKind == JsonValueKind.Number)
                            {
                                if (offerCardTypeEl.TryGetInt32(out var ov)) offerCardType = ov;
                            }
                            else if (offerCardTypeEl.ValueKind == JsonValueKind.String)
                            {
                                var s = offerCardTypeEl.GetString();
                                if (!string.IsNullOrWhiteSpace(s) && int.TryParse(s, out var ov2)) offerCardType = ov2;
                            }
                        }

                        static long? GetLongOrNull(JsonElement root, string prop)
                        {
                            if (!root.TryGetProperty(prop, out var el) || el.ValueKind == JsonValueKind.Null) return null;
                            if (el.ValueKind == JsonValueKind.Number)
                            {
                                if (el.TryGetInt64(out var n)) return n;
                                if (el.TryGetDouble(out var d)) return (long)d;
                            }
                            if (el.ValueKind == JsonValueKind.String)
                            {
                                var s = el.GetString();
                                if (!string.IsNullOrWhiteSpace(s) && long.TryParse(s, out var n2)) return n2;
                            }
                            return null;
                        }

                        static int? GetIntOrNull(JsonElement root, string prop)
                        {
                            if (!root.TryGetProperty(prop, out var el) || el.ValueKind == JsonValueKind.Null) return null;
                            if (el.ValueKind == JsonValueKind.Number)
                            {
                                if (el.TryGetInt32(out var n)) return n;
                                if (el.TryGetInt64(out var n64)) return (int)n64;
                            }
                            if (el.ValueKind == JsonValueKind.String)
                            {
                                var s = el.GetString();
                                if (!string.IsNullOrWhiteSpace(s) && int.TryParse(s, out var n2)) return n2;
                            }
                            return null;
                        }

                        static bool? GetBoolOrNull(JsonElement root, string prop)
                        {
                            if (!root.TryGetProperty(prop, out var el) || el.ValueKind == JsonValueKind.Null) return null;
                            if (el.ValueKind is JsonValueKind.True or JsonValueKind.False) return el.GetBoolean();
                            if (el.ValueKind == JsonValueKind.String)
                            {
                                var s = el.GetString();
                                if (string.IsNullOrWhiteSpace(s)) return null;
                                if (bool.TryParse(s, out var b)) return b;
                            }
                            return null;
                        }

                        static string? GetStringOrNull(JsonElement root, string prop)
                        {
                            if (!root.TryGetProperty(prop, out var el) || el.ValueKind == JsonValueKind.Null) return null;
                            if (el.ValueKind == JsonValueKind.String)
                            {
                                var s = el.GetString();
                                return string.IsNullOrWhiteSpace(s) ? null : s;
                            }
                            if (el.ValueKind == JsonValueKind.Number)
                            {
                                if (el.TryGetInt64(out var n)) return n.ToString();
                            }
                            return null;
                        }

                        long sold = 0L;
                        if (productData.TryGetProperty("sold", out var soldEl) && soldEl.ValueKind != JsonValueKind.Null)
                        {
                            if (soldEl.ValueKind == JsonValueKind.Number)
                            {
                                if (soldEl.TryGetInt64(out var sv)) sold = sv;
                            }
                            else if (soldEl.ValueKind == JsonValueKind.String)
                            {
                                var s = soldEl.GetString();
                                if (!string.IsNullOrWhiteSpace(s) && long.TryParse(s, out var sv2)) sold = sv2;
                            }
                        }

                        // Note: price/discount fields are persisted as raw numeric columns (PriceRaw/PriceBeforeDiscountRaw/...).

                        // Create product entity
                        var finalName = string.IsNullOrWhiteSpace(name) ? itemId : name;
                        var finalSlug = BuildProductSlug(finalName, itemId);

                        var product = new Product
                        {
                            Platform = "Shopee",
                            ExternalItemId = itemId,
                            ShopeeItemId = itemId,
                            ProductLink = string.IsNullOrWhiteSpace(productLink) ? null : productLink,
                            MaxCommissionRateRaw = string.IsNullOrWhiteSpace(maxCommissionRateRaw) ? null : maxCommissionRateRaw,
                            SellerCommissionRateRaw = string.IsNullOrWhiteSpace(sellerCommissionRateRaw) ? null : sellerCommissionRateRaw,
                            DefaultCommissionRateRaw = string.IsNullOrWhiteSpace(defaultCommissionRateRaw) ? null : defaultCommissionRateRaw,
                            ShopId = shopId,
                            ImagesJson = imagesJson,
                            VideoInfoListJson = videoInfoListJson,
                            TierVariationsJson = tierVariationsJson,
                            ItemRatingJson = itemRatingJson,
                            OfferCardType = offerCardType,
                            Currency = GetStringOrNull(productData, "currency"),
                            Stock = GetLongOrNull(productData, "stock"),
                            Status = GetIntOrNull(productData, "status"),
                            Ctime = GetLongOrNull(productData, "ctime"),
                            SoldText = GetStringOrNull(productData, "sold_text"),
                            CatId = GetLongOrNull(productData, "catid"),
                            ItemStatus = GetStringOrNull(productData, "item_status"),
                            PriceRaw = GetLongOrNull(productData, "price"),
                            PriceMinRaw = GetLongOrNull(productData, "price_min"),
                            PriceMaxRaw = GetLongOrNull(productData, "price_max"),
                            PriceMinBeforeDiscountRaw = GetLongOrNull(productData, "price_min_before_discount"),
                            PriceMaxBeforeDiscountRaw = GetLongOrNull(productData, "price_max_before_discount"),
                            PriceBeforeDiscountRaw = GetLongOrNull(productData, "price_before_discount"),
                            Discount = GetStringOrNull(productData, "discount"),
                            ItemType = GetIntOrNull(productData, "item_type"),
                            ShopeeVerified = GetBoolOrNull(productData, "shopee_verified"),
                            IsOfficialShop = GetBoolOrNull(productData, "is_official_shop"),
                            ShopLocation = GetStringOrNull(productData, "shop_location"),
                            CanUseCod = GetBoolOrNull(productData, "can_use_cod"),
                            IsOnFlashSale = GetBoolOrNull(productData, "is_on_flash_sale"),
                            ShopName = GetStringOrNull(productData, "shop_name"),
                            ShopRating = productData.TryGetProperty("shop_rating", out var shopRatingEl) && shopRatingEl.ValueKind != JsonValueKind.Null
                                ? (shopRatingEl.TryGetSingle(out var sr) ? sr : (shopRatingEl.TryGetDouble(out var srd) ? (float)srd : null))
                                : null,
                            Name = finalName,
                            Slug = finalSlug,
                            // Store only image key in DB. CDN prefix is applied when returning data.
                            ImageUrl = string.IsNullOrWhiteSpace(image) ? null : image,
                            SoldRaw = sold,
                            LastSynced = DateTime.UtcNow,
                            CreatedAt = DateTime.UtcNow,
                        };

                        // Save to database
                        await _productRepository.UpsertByShopeeItemIdAsync(product);

                        if (!string.IsNullOrWhiteSpace(itemId) && !string.IsNullOrWhiteSpace(productLink))
                        {
                            var existingAffiliate = await _productAffiliateLinks.GetByExternalItemIdAsync(itemId);
                            if (existingAffiliate == null)
                            {
                                await _productAffiliateLinks.UpsertAsync(new ProductAffiliateLink
                                {
                                    ExternalItemId = itemId,
                                    ProductLink = productLink,
                                    AffiliateLink = null,
                                });
                            }
                        }
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"Item ?: {ex.Message}");
                        errorCount++;
                        continue;
                    }
                }

                var message = $"Import completed: {successCount} successful, {errorCount} failed";
                if (errors.Count > 0)
                {
                    message += ". Errors: " + string.Join("; ", errors.Take(5));
                    if (errors.Count > 5)
                    {
                        message += $"... and {errors.Count - 5} more errors";
                    }
                }

                return Ok(new { message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Import failed: {ex.Message}");
            }
        }

        private static string BuildProductSlug(string? name, string? uniqueSuffix)
        {
            var baseName = string.IsNullOrWhiteSpace(name) ? "product" : name;
            var normalized = baseName.ToLowerInvariant();
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[^a-z0-9\s-]", "");
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", "-");
            normalized = normalized.Trim('-');

            if (string.IsNullOrWhiteSpace(normalized)) normalized = "product";
            if (!string.IsNullOrWhiteSpace(uniqueSuffix)) normalized = normalized + "-" + uniqueSuffix.Trim();
            return normalized;
        }

        private string GetShopeeCdnImagePrefix()
        {
            var prefix = _config["Shopee:CdnImagePrefix"];
            if (string.IsNullOrWhiteSpace(prefix)) prefix = "https://down-vn.img.susercontent.com/file/";
            return prefix.EndsWith("/") ? prefix : prefix + "/";
        }

        private string GetShopeeCdnVideoPrefix()
        {
            var prefix = _config["Shopee:CdnVideoPrefix"];
            if (string.IsNullOrWhiteSpace(prefix)) prefix = "https://down-tx-sg.vod.susercontent.com/";
            return prefix.EndsWith("/") ? prefix : prefix + "/";
        }

        private static string[] ParseStringArrayJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return Array.Empty<string>();
                var list = new List<string>();
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    if (el.ValueKind == JsonValueKind.String)
                    {
                        var s = el.GetString();
                        if (!string.IsNullOrWhiteSpace(s)) list.Add(s);
                    }
                    else if (el.ValueKind == JsonValueKind.Number)
                    {
                        if (el.TryGetInt64(out var n)) list.Add(n.ToString());
                    }
                }
                return list.ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static string[] ExtractVideoUrls(string? videoInfoListJson, string videoPrefix)
        {
            if (string.IsNullOrWhiteSpace(videoInfoListJson)) return Array.Empty<string>();
            try
            {
                using var doc = JsonDocument.Parse(videoInfoListJson);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) return Array.Empty<string>();

                var urls = new List<string>();
                foreach (var v in doc.RootElement.EnumerateArray())
                {
                    if (v.ValueKind != JsonValueKind.Object) continue;
                    if (!v.TryGetProperty("default_format", out var defaultFormat) || defaultFormat.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (defaultFormat.TryGetProperty("url", out var urlEl) && urlEl.ValueKind == JsonValueKind.String)
                    {
                        var u = urlEl.GetString();
                        if (!string.IsNullOrWhiteSpace(u)) urls.Add(u);
                        continue;
                    }

                    if (defaultFormat.TryGetProperty("path", out var pathEl) && pathEl.ValueKind == JsonValueKind.String)
                    {
                        var p = pathEl.GetString();
                        if (!string.IsNullOrWhiteSpace(p))
                        {
                            urls.Add(BuildCdnUrl(videoPrefix, p) ?? string.Empty);
                        }
                    }
                }

                return urls.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static string? BuildCdnUrl(string prefix, string? stored)
        {
            if (string.IsNullOrWhiteSpace(stored)) return null;
            if (stored.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || stored.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return stored;
            }
            return prefix + stored.TrimStart('/');
        }

        private string GenerateAffiliateLink(Product product, long clickId)
        {
            // This is a placeholder implementation
            // In reality, you'd generate platform-specific affiliate links
            // For Shopee: https://shopee.vn/product/{shopId}/{itemId}?affiliate_id={clickId}
            // For Lazada: https://www.lazada.vn/products/{productId}.html?affiliate_id={clickId}
            
            return $"https://shopee.vn/product/{product.ShopeeItemId}?affiliate_id={clickId}";
        }
    }

    public class BuyNowRequest
    {
        public string Platform { get; set; } = "Shopee";
        public string ExternalItemId { get; set; } = string.Empty;
        public string ShopeeItemId { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Slug { get; set; }
        public string? ImageUrl { get; set; }
        public decimal? Price { get; set; }
        public decimal? OriginalPrice { get; set; }
        public float? Rating { get; set; }
        public long Sold { get; set; }
        public float? CommissionRate { get; set; }
        public string? IPAddress { get; set; }
        public string? UserAgent { get; set; }
    }

    public class ImportRequest
    {
        public string Data { get; set; } = string.Empty;
    }

    public class ImportProductItem
    {
        public string ItemId { get; set; } = string.Empty;
        public string ProductLink { get; set; } = string.Empty;
        public bool IsFreeSample { get; set; }
        public string MaxCommissionRate { get; set; } = string.Empty;
        public string SellerCommissionRate { get; set; } = string.Empty;
        public string DefaultCommissionRate { get; set; } = string.Empty;
        public BatchItemData? BatchItemForItemCardFull { get; set; }
        public bool IsSellerInvited { get; set; }
        public int OfferCardType { get; set; }
        public object? InvitedCampaignOfferCard { get; set; }
        public object[]? PromotionVouchers { get; set; }
    }

    public class BatchItemData
    {
        public string Itemid { get; set; } = string.Empty;
        public long Shopid { get; set; }
        public string Name { get; set; } = string.Empty;
        public string[] LabelIds { get; set; } = Array.Empty<string>();
        public string Image { get; set; } = string.Empty;
        public string[] Images { get; set; } = Array.Empty<string>();
        public string Currency { get; set; } = string.Empty;
        public long Stock { get; set; }
        public int Status { get; set; }
        public long Ctime { get; set; }
        public long Sold { get; set; }
        public string SoldText { get; set; } = string.Empty;
        public long HistoricalSold { get; set; }
        public string HistoricalSoldText { get; set; } = string.Empty;
        public bool Liked { get; set; }
        public long LikedCount { get; set; }
        public string ViewCount { get; set; } = string.Empty;
        public long Catid { get; set; }
        public string Brand { get; set; } = string.Empty;
        public long CmtCount { get; set; }
        public long Flag { get; set; }
        public int CbOption { get; set; }
        public string ItemStatus { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
        public string PriceMin { get; set; } = string.Empty;
        public string PriceMax { get; set; } = string.Empty;
        public string PriceMinBeforeDiscount { get; set; } = string.Empty;
        public string PriceMaxBeforeDiscount { get; set; } = string.Empty;
        public string HiddenPriceDisplay { get; set; } = string.Empty;
        public string PriceBeforeDiscount { get; set; } = string.Empty;
        public bool HasLowestPriceGuarantee { get; set; }
        public int ShowDiscount { get; set; }
        public int RawDiscount { get; set; }
        public string Discount { get; set; } = string.Empty;
        public bool IsCategoryFailed { get; set; }
        public string SizeChart { get; set; } = string.Empty;
        public VideoInfo[]? VideoInfoList { get; set; }
        public TierVariation[]? TierVariations { get; set; }
        public ItemRating ItemRating { get; set; } = new();
        public int ItemType { get; set; }
        public string ReferenceItemId { get; set; } = string.Empty;
        public string TransparentBackgroundImage { get; set; } = string.Empty;
        public bool IsAdult { get; set; }
        public int BadgeIconType { get; set; }
        public bool ShopeeVerified { get; set; }
        public bool IsOfficialShop { get; set; }
        public bool ShowOfficialShopLabel { get; set; }
        public bool ShowShopeeVerifiedLabel { get; set; }
        public bool ShowOfficialShopLabelInTitle { get; set; }
        public bool IsCcInstallmentPaymentEligible { get; set; }
        public bool IsNonCcInstallmentPaymentEligible { get; set; }
        public string CoinEarnLabel { get; set; } = string.Empty;
        public bool ShowFreeShipping { get; set; }
        public object? PreviewInfo { get; set; }
        public object? CoinInfo { get; set; }
        public bool CanUseBundleDeal { get; set; }
        public object? BundleDealInfo { get; set; }
        public bool IsGroupBuyItem { get; set; }
        public bool HasGroupBuyStock { get; set; }
        public object? GroupBuyInfo { get; set; }
        public int WelcomePackageType { get; set; }
        public object? WelcomePackageInfo { get; set; }
        public AddOnDealInfo? AddOnDealInfo { get; set; }
        public bool CanUseWholesale { get; set; }
        public bool IsPreferredPlusSeller { get; set; }
        public string ShopLocation { get; set; } = string.Empty;
        public bool HasModelWithAvailableShopeeStock { get; set; }
        public VoucherInfo? VoucherInfo { get; set; }
        public bool CanUseCod { get; set; }
        public bool IsOnFlashSale { get; set; }
        public int SplInstallmentTenure { get; set; }
        public bool IsLiveStreamingPrice { get; set; }
        public string ShopName { get; set; } = string.Empty;
        public float ShopRating { get; set; }
        public object? DeepDiscountSkin { get; set; }
        public bool IsMart { get; set; }
        public string PackSize { get; set; } = string.Empty;
        public object[]? OverlayImages { get; set; }
        public object[]? OptimizedNames { get; set; }
        public object? LiveStreamSession { get; set; }
        public string BundleDealId { get; set; } = string.Empty;
    }

    public class VideoInfo
    {
        public Format[] Formats { get; set; } = Array.Empty<Format>();
        public string VideoId { get; set; } = string.Empty;
        public string ThumbUrl { get; set; } = string.Empty;
        public int Duration { get; set; }
        public int Version { get; set; }
        public string Vid { get; set; } = string.Empty;
        public DefaultFormat DefaultFormat { get; set; } = new();
    }

    public class Format
    {
        public string Defn { get; set; } = string.Empty;
        public string Profile { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public class DefaultFormat
    {
        public int Format { get; set; }
        public string Defn { get; set; } = string.Empty;
        public string Profile { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
    }

    public class TierVariation
    {
        public string[] Options { get; set; } = Array.Empty<string>();
        public string[] Images { get; set; } = Array.Empty<string>();
        public object[] Properties { get; set; } = Array.Empty<object>();
        public string Name { get; set; } = string.Empty;
        public int Type { get; set; }
    }

    public class ItemRating
    {
        public long[] RatingCount { get; set; } = Array.Empty<long>();
        public float RatingStar { get; set; }
        public long RcountWithContext { get; set; }
        public long RcountWithImage { get; set; }
    }

    public class AddOnDealInfo
    {
        public string AddOnDealId { get; set; } = string.Empty;
        public string AddOnDealLabel { get; set; } = string.Empty;
        public int SubType { get; set; }
        public int Status { get; set; }
    }

    public class VoucherInfo
    {
        public long PromotionId { get; set; }
        public string VoucherCode { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
    }
}
