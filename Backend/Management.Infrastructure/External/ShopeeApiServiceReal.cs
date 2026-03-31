using Management.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace Management.Infrastructure.External
{
    public interface IShopeeApiServiceReal
    {
        Task<IEnumerable<Product>> SearchProductsAsync(string query, int limit = 20, long? categoryId = null, CancellationToken cancellationToken = default);
        Task<Product?> GetProductByIdAsync(long itemId, CancellationToken cancellationToken = default);
    }

    public class ShopeeApiServiceReal : IShopeeApiServiceReal
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ShopeeApiServiceReal> _logger;
        private readonly IConfiguration _config;

        // Simple in-memory cache (1 minute)
        private static readonly ConcurrentDictionary<string, (DateTime ExpiresAtUtc, string Json)> _cache = new();

        public ShopeeApiServiceReal(HttpClient httpClient, ILogger<ShopeeApiServiceReal> logger, IConfiguration config)
        {
            _httpClient = httpClient;
            _logger = logger;
            _config = config;

            // Set default headers to mimic browser
            _httpClient.DefaultRequestHeaders.Accept.Clear();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.AcceptLanguage.Clear();
            _httpClient.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("vi-VN"));
            _httpClient.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("vi", 0.9));
            _httpClient.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en-US", 0.8));
            _httpClient.DefaultRequestHeaders.AcceptLanguage.Add(new StringWithQualityHeaderValue("en", 0.7));
            _httpClient.DefaultRequestHeaders.UserAgent.Clear();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/145.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("x-api-source", "pc");
            _httpClient.DefaultRequestHeaders.Add("x-shopee-language", "vi");
            _httpClient.DefaultRequestHeaders.Add("x-sz-sdk-version", "1.12.27");
        }

        public async Task<IEnumerable<Product>> SearchProductsAsync(string query, int limit = 20, long? categoryId = null, CancellationToken cancellationToken = default)
        {
            try
            {
                await Task.CompletedTask;
                return Enumerable.Empty<Product>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Shopee search API");
                return Enumerable.Empty<Product>();
            }
        }

        public async Task<Product?> GetProductByIdAsync(long itemId, CancellationToken cancellationToken = default)
        {
            try
            {
                await Task.CompletedTask;
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Shopee item detail API for itemid {ItemId}", itemId);
                return null;
            }
        }

    }
}
