using Management.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Management.Infrastructure.External
{
    public interface IAccessTradeApiService
    {
        Task<JsonDocument> GetOrderListAsync(
            DateTime since,
            DateTime until,
            int page = 1,
            string? utmSource = null,
            string? utmCampaign = null,
            string? utmMedium = null,
            string? utmContent = null,
            int? limit = null,
            int? status = null,
            string? merchant = null,
            CancellationToken cancellationToken = default);
        Task<IEnumerable<Product>> GetDataFeedsAsync(
            string domain,
            string? category = null,
            int page = 1,
            int limit = 50,
            CancellationToken cancellationToken = default);
    }

    public class AccessTradeApiService : IAccessTradeApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly ILogger<AccessTradeApiService> _logger;

        private static readonly object _rateLock = new();
        private static readonly Queue<DateTime> _requestTimestampsUtc = new();

        private static readonly ConcurrentDictionary<string, (DateTime ExpiresAtUtc, string Json)> _cache = new();

        public AccessTradeApiService(HttpClient httpClient, IConfiguration config, ILogger<AccessTradeApiService> logger)
        {
            _httpClient = httpClient;
            _config = config;
            _logger = logger;

            var baseUrl = _config["AccessTrade:BaseUrl"];
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                baseUrl = "https://api.accesstrade.vn/";
            }

            if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
            {
                throw new InvalidOperationException("Invalid AccessTrade:BaseUrl configuration");
            }

            _httpClient.BaseAddress = baseUri;
        }

        public async Task<JsonDocument> GetOrderListAsync(
            DateTime since,
            DateTime until,
            int page = 1,
            string? utmSource = null,
            string? utmCampaign = null,
            string? utmMedium = null,
            string? utmContent = null,
            int? limit = null,
            int? status = null,
            string? merchant = null,
            CancellationToken cancellationToken = default)
        {
            var accessKey = _config["AccessTrade:AccessKey"];
            if (string.IsNullOrWhiteSpace(accessKey))
            {
                throw new InvalidOperationException("Missing AccessTrade:AccessKey configuration");
            }

            // AccessTrade notes:
            // - Limit: 10 requests / minute
            // - Cache: 1 minute
            var url = BuildOrderListUrl(since, until, page, utmSource, utmCampaign, utmMedium, utmContent, limit, status, merchant);
            var cacheKey = url;

            if (TryGetCache(cacheKey, out var cachedJson))
            {
                return JsonDocument.Parse(cachedJson);
            }

            await EnforceRateLimitAsync(cancellationToken);

            // AccessTrade expects: Authorization: Token <access_key>
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Token", accessKey);

            try
            {
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("AccessTrade order-list failed: {StatusCode}. Body: {Body}", (int)response.StatusCode, body);
                    throw new HttpRequestException($"AccessTrade order-list failed with status {(int)response.StatusCode}");
                }

                SetCache(cacheKey, body, TimeSpan.FromMinutes(1));
                return JsonDocument.Parse(body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling AccessTrade order-list");
                throw;
            }
        }

        public async Task<IEnumerable<Product>> GetDataFeedsAsync(
            string domain,
            string? category = null,
            int page = 1,
            int limit = 50,
            CancellationToken cancellationToken = default)
        {
            var accessKey = _config["AccessTrade:AccessKey"];
            if (string.IsNullOrWhiteSpace(accessKey))
            {
                throw new InvalidOperationException("Missing AccessTrade:AccessKey configuration");
            }
            limit = 1000;
            var url = BuildDataFeedsUrl(domain, category, page, limit);
            var cacheKey = url;

            if (TryGetCache(cacheKey, out var cachedJson))
            {
                return ParseDataFeedsResponse(cachedJson);
            }

            await EnforceRateLimitAsync(cancellationToken);

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Token", accessKey);

            try
            {
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("AccessTrade datafeeds failed: {StatusCode}. Body: {Body}", (int)response.StatusCode, body);
                    return Enumerable.Empty<Product>();
                }

                SetCache(cacheKey, body, TimeSpan.FromMinutes(1));
                return ParseDataFeedsResponse(body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling AccessTrade datafeeds");
                return Enumerable.Empty<Product>();
            }
        }

        private static string BuildOrderListUrl(
            DateTime since,
            DateTime until,
            int page,
            string? utmSource,
            string? utmCampaign,
            string? utmMedium,
            string? utmContent,
            int? limit,
            int? status,
            string? merchant)
        {
            var sb = new StringBuilder();
            sb.Append("v1/order-list");

            var hasQuery = false;
            void Add(string key, string value)
            {
                sb.Append(hasQuery ? '&' : '?');
                hasQuery = true;
                sb.Append(Uri.EscapeDataString(key));
                sb.Append('=');
                sb.Append(Uri.EscapeDataString(value));
            }

            // Required ISO format, example: 2021-01-01T00:00:00Z
            Add("since", ToIsoUtcZ(since));
            Add("until", ToIsoUtcZ(until));

            if (page > 0) Add("page", page.ToString());

            if (!string.IsNullOrWhiteSpace(utmSource)) Add("utm_source", utmSource);
            if (!string.IsNullOrWhiteSpace(utmCampaign)) Add("utm_campaign", utmCampaign);
            if (!string.IsNullOrWhiteSpace(utmMedium)) Add("utm_medium", utmMedium);
            if (!string.IsNullOrWhiteSpace(utmContent)) Add("utm_content", utmContent);

            if (limit.HasValue)
            {
                var normalized = Math.Clamp(limit.Value, 1, 300);
                Add("limit", normalized.ToString());
            }

            if (status.HasValue) Add("status", status.Value.ToString());
            if (!string.IsNullOrWhiteSpace(merchant)) Add("merchant", merchant);

            return sb.ToString();
        }

        private static string BuildDataFeedsUrl(string domain, string? category, int page, int limit)
        {
            var sb = new StringBuilder();
            sb.Append("v1/datafeeds");

            var hasQuery = false;
            void Add(string key, string value)
            {
                sb.Append(hasQuery ? '&' : '?');
                hasQuery = true;
                sb.Append(Uri.EscapeDataString(key));
                sb.Append('=');
                sb.Append(Uri.EscapeDataString(value));
            }

            Add("domain", domain);
            if (page > 0) Add("page", page.ToString());
            if (limit > 0) Add("limit", Math.Clamp(limit, 1, 300).ToString());
            if (!string.IsNullOrWhiteSpace(category)) Add("cat", category);

            return sb.ToString();
        }

        private static string ToIsoUtcZ(DateTime value)
        {
            var utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
            return utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        }

        private static bool TryGetCache(string key, out string json)
        {
            json = string.Empty;
            if (_cache.TryGetValue(key, out var entry))
            {
                if (entry.ExpiresAtUtc > DateTime.UtcNow)
                {
                    json = entry.Json;
                    return true;
                }

                _cache.TryRemove(key, out _);
            }

            return false;
        }

        private static void SetCache(string key, string json, TimeSpan ttl)
        {
            _cache[key] = (DateTime.UtcNow.Add(ttl), json);
        }

        private static async Task EnforceRateLimitAsync(CancellationToken cancellationToken)
        {
            // 10 requests / minute (rolling window)
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int delayMs;
                lock (_rateLock)
                {
                    var now = DateTime.UtcNow;
                    while (_requestTimestampsUtc.Count > 0 && (now - _requestTimestampsUtc.Peek()).TotalSeconds >= 60)
                    {
                        _requestTimestampsUtc.Dequeue();
                    }

                    if (_requestTimestampsUtc.Count < 10)
                    {
                        _requestTimestampsUtc.Enqueue(now);
                        return;
                    }

                    var oldest = _requestTimestampsUtc.Peek();
                    var wait = oldest.AddSeconds(60) - now;
                    delayMs = (int)Math.Ceiling(Math.Max(wait.TotalMilliseconds, 50));
                }

                await Task.Delay(delayMs, cancellationToken);
            }
        }

        private static IEnumerable<Product> ParseDataFeedsResponse(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var list = new List<Product>();

                // Try common array keys
                JsonElement items;
                if (doc.RootElement.TryGetProperty("data", out items) && items.ValueKind == JsonValueKind.Array)
                {
                    // ok
                }
                else if (doc.RootElement.TryGetProperty("items", out items) && items.ValueKind == JsonValueKind.Array)
                {
                    // ok
                }
                else if (doc.RootElement.TryGetProperty("result", out items) && items.ValueKind == JsonValueKind.Array)
                {
                    // ok
                }
                else if (doc.RootElement.TryGetProperty("products", out items) && items.ValueKind == JsonValueKind.Array)
                {
                    // ok
                }
                else
                {
                    // Log raw for debugging
                    Console.WriteLine($"AccessTrade datafeeds raw response: {json}");
                    return Enumerable.Empty<Product>();
                }

                foreach (var item in items.EnumerateArray())
                {
                    var productId = item.TryGetProperty("product_id", out var pidEl) ? pidEl.GetString() : string.Empty;
                    var name = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : string.Empty;
                    var url = item.TryGetProperty("url", out var urlEl) ? urlEl.GetString() : string.Empty;
                    var affLink = item.TryGetProperty("aff_link", out var affEl) ? affEl.GetString() : string.Empty;
                    var image = item.TryGetProperty("image", out var imgEl) ? imgEl.GetString() : null;
                    var price = item.TryGetProperty("price", out var priceEl) && priceEl.ValueKind != JsonValueKind.Null
                        ? (decimal?)priceEl.GetDecimal()
                        : null;
                    var discount = item.TryGetProperty("discount", out var discEl) && discEl.ValueKind != JsonValueKind.Null
                        ? (decimal?)discEl.GetDecimal()
                        : null;
                    var campaign = item.TryGetProperty("campaign", out var campEl) ? campEl.GetString() : string.Empty;
                    var domain = item.TryGetProperty("domain", out var domainEl) ? domainEl.GetString() : string.Empty;
                    var cate = item.TryGetProperty("cate", out var cateEl) ? cateEl.GetString() : string.Empty;
                    var desc = item.TryGetProperty("desc", out var descEl) ? descEl.GetString() : null;
                    var sku = item.TryGetProperty("sku", out var skuEl) ? skuEl.GetString() : string.Empty;
                    var statusDiscount = item.TryGetProperty("status_discount", out var statEl) && statEl.ValueKind != JsonValueKind.Null
                        ? (int?)statEl.GetInt32()
                        : null;
                    var discountAmount = item.TryGetProperty("discount_amount", out var discAmtEl) && discAmtEl.ValueKind != JsonValueKind.Null
                        ? (decimal?)discAmtEl.GetDecimal()
                        : null;
                    var discountRate = item.TryGetProperty("discount_rate", out var discRateEl) && discRateEl.ValueKind != JsonValueKind.Null
                        ? (float?)discRateEl.GetSingle()
                        : null;
                    var updateTimeStr = item.TryGetProperty("update_time", out var updEl) ? updEl.GetString() : null;
                    DateTime? updateTime = null;
                    if (!string.IsNullOrWhiteSpace(updateTimeStr) && DateTime.TryParse(updateTimeStr, out var updDt))
                    {
                        updateTime = updDt;
                    }

                    // Use discount as commission placeholder; you can change if API provides commission
                    var commissionRate = discountRate; // percentage discount as commission placeholder

                    static long? ToRawPrice(decimal? v)
                    {
                        if (v == null) return null;
                        try
                        {
                            return (long)(v.Value * 1000m);
                        }
                        catch
                        {
                            return null;
                        }
                    }

                    list.Add(new Product
                    {
                        Id = 0,
                        Platform = "AccessTrade",
                        ExternalItemId = productId,
                        Name = name,
                        Slug = GenerateSlug(name),
                        ImageUrl = image,
                        PriceRaw = ToRawPrice(discount ?? price), // prefer discounted price
                        PriceBeforeDiscountRaw = ToRawPrice(price),
                        DefaultCommissionRateRaw = commissionRate.HasValue ? (commissionRate.Value.ToString() + "%") : null,
                        LastSynced = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                    });
                }

                return list;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing AccessTrade datafeeds response: {ex.Message}");
                Console.WriteLine($"Raw response: {json}");
                return Enumerable.Empty<Product>();
            }
        }

        private static string GenerateSlug(string name)
        {
            var normalized = name.ToLowerInvariant();
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[^a-z0-9\s-]", "");
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", "-");
            return normalized.Trim('-');
        }
    }
}
