using Management.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;

namespace Management.Infrastructure.External
{
    public interface IShopeeOpenApiService
    {
        Task<IEnumerable<Product>> SearchItemsAsync(string query, int page = 1, int pageSize = 20, long? categoryId = null, string? sort = null, CancellationToken cancellationToken = default);
        Task<Product?> GetItemDetailAsync(long itemId, CancellationToken cancellationToken = default);
        Task<string?> GenerateAffiliateLinkAsync(long itemId, CancellationToken cancellationToken = default);
    }

    public class ShopeeOpenApiService : IShopeeOpenApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly ILogger<ShopeeOpenApiService> _logger;

        // Cache 1 minute
        private static readonly ConcurrentDictionary<string, (DateTime ExpiresAtUtc, string Json)> _cache = new();

        public ShopeeOpenApiService(HttpClient httpClient, IConfiguration config, ILogger<ShopeeOpenApiService> logger)
        {
            _httpClient = httpClient;
            _config = config;
            _logger = logger;

            var baseUrl = _config["ShopeeOpenApi:BaseUrl"] ?? "https://affiliate.shopee.vn/api";
            _httpClient.BaseAddress = new Uri(baseUrl);
        }

        public async Task<IEnumerable<Product>> SearchItemsAsync(string query, int page = 1, int pageSize = 20, long? categoryId = null, string? sort = null, CancellationToken cancellationToken = default)
        {
            try
            {
                await Task.CompletedTask;
                return Enumerable.Empty<Product>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Shopee Open API search");
                return Enumerable.Empty<Product>();
            }
        }

        public async Task<Product?> GetItemDetailAsync(long itemId, CancellationToken cancellationToken = default)
        {
            try
            {
                await Task.CompletedTask;
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Shopee Open API item detail");
                return null;
            }
        }

        public async Task<string?> GenerateAffiliateLinkAsync(long itemId, CancellationToken cancellationToken = default)
        {
            var appId = _config["ShopeeOpenApi:AppId"];
            var apiKey = _config["ShopeeOpenApi:ApiKey"];
            if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("Missing ShopeeOpenApi:AppId or ShopeeOpenApi:ApiKey configuration");
            }

            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var path = "/api/v2/product/link_generator";
            var queryDict = new Dictionary<string, string>
            {
                ["appid"] = appId,
                ["timestamp"] = timestamp.ToString(),
                ["itemid"] = itemId.ToString(),
            };
            var sign = GenerateSign(path, queryDict, apiKey);
            queryDict["sign"] = sign;

            var url = BuildUrl(path, queryDict);
            try
            {
                var response = await _httpClient.GetAsync(url, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Shopee Open API link generator failed: {StatusCode}. Body: {Body}", (int)response.StatusCode, body);
                    return null;
                }

                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("data", out var dataEl) && dataEl.TryGetProperty("affiliate_link", out var affEl))
                {
                    return affEl.GetString();
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Shopee Open API link generator");
                return null;
            }
        }

        private static string GenerateSign(string path, Dictionary<string, string> query, string apiKey)
        {
            // Build canonical query string: keys sorted, URL-encoded
            var sorted = query.OrderBy(kv => kv.Key);
            var qs = string.Join("&", sorted.Select(kv => $"{kv.Key}={kv.Value}"));
            var toSign = $"{path}{qs}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(toSign));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static string BuildUrl(string path, Dictionary<string, string> query)
        {
            var qs = string.Join("&", query.Select(kv => $"{kv.Key}={HttpUtility.UrlEncode(kv.Value)}"));
            return $"{path}?{qs}";
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

        private static IEnumerable<Product> ParseSearchResponse(string json)
        {
            return Enumerable.Empty<Product>();
        }

        private static Product? ParseDetailResponse(string json)
        {
            return null;
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
