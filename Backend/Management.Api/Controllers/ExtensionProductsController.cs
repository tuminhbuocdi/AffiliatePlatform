using System.Text.Json;
using System.Text.RegularExpressions;
using Management.Domain.Entities;
using Management.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Management.Api.Controllers
{
    [ApiController]
    [Route("api/extension/products")]
    public class ExtensionProductsController : ControllerBase
    {
        private readonly IProductRepository _products;
        private readonly ProductAffiliateLinkRepository _productAffiliateLinks;

        public ExtensionProductsController(IProductRepository products, ProductAffiliateLinkRepository productAffiliateLinks)
        {
            _products = products;
            _productAffiliateLinks = productAffiliateLinks;
        }

        [HttpPost("import-and-affiliate")]
        public async Task<IActionResult> ImportAndAffiliate([FromBody] JsonElement request)
        {
            JsonElement itemsRoot;
            if (request.ValueKind == JsonValueKind.Array)
            {
                itemsRoot = request;
            }
            else if (request.ValueKind == JsonValueKind.Object
                && request.TryGetProperty("data", out var dataEl)
                && dataEl.ValueKind == JsonValueKind.Object
                && dataEl.TryGetProperty("list", out var listEl)
                && listEl.ValueKind == JsonValueKind.Array)
            {
                itemsRoot = listEl;
            }
            else
            {
                return BadRequest("Request must be a JSON array or an object containing data.list array.");
            }

            var items = itemsRoot.EnumerateArray().ToList();
            if (items.Count == 0)
            {
                return BadRequest("No items found in request.");
            }

            var results = new List<object>();
            var errors = new List<string>();

            foreach (var item in items)
            {
                try
                {
                    var product = BuildProductFromImportItem(item);
                    var saved = await _products.UpsertByShopeeItemIdAsync(product);

                    if (!string.IsNullOrWhiteSpace(saved.ExternalItemId) && !string.IsNullOrWhiteSpace(saved.ProductLink))
                    {
                        var existingAffiliate = await _productAffiliateLinks.GetByExternalItemIdAsync(saved.ExternalItemId);
                        if (existingAffiliate == null)
                        {
                            await _productAffiliateLinks.UpsertAsync(new ProductAffiliateLink
                            {
                                ExternalItemId = saved.ExternalItemId,
                                ProductLink = saved.ProductLink,
                                AffiliateLink = null,
                            });
                        }
                    }

                    results.Add(new
                    {
                        id = saved.Id,
                        itemId = saved.ShopeeItemId,
                        externalItemId = saved.ExternalItemId,
                        name = saved.Name,
                        productLink = saved.ProductLink,
                    });
                }
                catch (Exception ex)
                {
                    errors.Add($"Item {TryGetImportItemId(item) ?? "?"}: {ex.Message}");
                }
            }

            return Ok(new
            {
                message = "Cào dữ liệu thành công",
                importedCount = results.Count,
                errorCount = errors.Count,
                items = results,
                errors,
            });
        }

        private static Product BuildProductFromImportItem(JsonElement item)
        {
            if (item.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("Invalid item format.");

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

            if (string.IsNullOrWhiteSpace(itemId))
            {
                itemId = productData.TryGetProperty("itemid", out var nestedItemIdEl) ? GetStringOrNumberAsString(nestedItemIdEl) : string.Empty;
            }

            if (string.IsNullOrWhiteSpace(itemId))
                throw new InvalidOperationException("item_id is required.");

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

            var finalName = string.IsNullOrWhiteSpace(name) ? itemId : name;
            var finalSlug = BuildProductSlug(finalName, itemId);

            return new Product
            {
                Platform = "Shopee",
                ExternalItemId = itemId,
                ShopeeItemId = itemId,
                Name = finalName,
                Slug = finalSlug,
                ImageUrl = string.IsNullOrWhiteSpace(image) ? null : image,

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
                SoldRaw = sold,
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
                ShopRating = GetFloatOrNull(productData, "shop_rating"),

                LastSynced = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            };
        }

        private static long? GetLongOrNull(JsonElement root, string prop)
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

        private static int? GetIntOrNull(JsonElement root, string prop)
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

        private static bool? GetBoolOrNull(JsonElement root, string prop)
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

        private static string? GetStringOrNull(JsonElement root, string prop)
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

        private static float? GetFloatOrNull(JsonElement root, string prop)
        {
            if (!root.TryGetProperty(prop, out var el) || el.ValueKind == JsonValueKind.Null) return null;
            if (el.ValueKind == JsonValueKind.Number)
            {
                if (el.TryGetSingle(out var f)) return f;
                if (el.TryGetDouble(out var d)) return (float)d;
            }
            if (el.ValueKind == JsonValueKind.String)
            {
                var s = el.GetString();
                if (!string.IsNullOrWhiteSpace(s) && float.TryParse(s, out var f2)) return f2;
            }
            return null;
        }

        private static string? TryGetImportItemId(JsonElement item)
        {
            if (item.ValueKind != JsonValueKind.Object) return null;
            if (item.TryGetProperty("item_id", out var itemIdEl)) return GetStringOrNumberAsString(itemIdEl);
            if (item.TryGetProperty("itemid", out var itemIdEl2)) return GetStringOrNumberAsString(itemIdEl2);
            if (item.TryGetProperty("batch_item_for_item_card_full", out var batchEl)
                && batchEl.ValueKind == JsonValueKind.Object)
            {
                if (batchEl.TryGetProperty("itemid", out var nestedItemIdEl)) return GetStringOrNumberAsString(nestedItemIdEl);
                if (batchEl.TryGetProperty("item_id", out var nestedItemIdEl2)) return GetStringOrNumberAsString(nestedItemIdEl2);
            }
            return null;
        }

        private static string GetStringOrNumberAsString(JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.String) return el.GetString() ?? string.Empty;
            if (el.ValueKind == JsonValueKind.Number)
            {
                if (el.TryGetInt64(out var n)) return n.ToString();
            }
            return string.Empty;
        }

        private static string BuildProductSlug(string? name, string? uniqueSuffix)
        {
            var baseName = string.IsNullOrWhiteSpace(name) ? "product" : name;
            var normalized = baseName.ToLowerInvariant();
            normalized = Regex.Replace(normalized, @"[^a-z0-9\s-]", "");
            normalized = Regex.Replace(normalized, @"\s+", "-");
            normalized = normalized.Trim('-');

            if (string.IsNullOrWhiteSpace(normalized)) normalized = "product";
            if (!string.IsNullOrWhiteSpace(uniqueSuffix)) normalized = normalized + "-" + uniqueSuffix.Trim();
            return normalized;
        }
    }
}
