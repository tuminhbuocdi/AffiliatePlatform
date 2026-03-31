using Management.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Net.Http;

namespace Management.Infrastructure.External
{
    public interface IShopeeApiService
    {
        Task<IEnumerable<Product>> SearchProductsAsync(string query, int limit = 20);
        Task<Product?> GetProductByIdAsync(long itemId);
        Task<IEnumerable<Category>> GetCategoriesAsync(int limit = 50);
    }

    public class ShopeeApiService : IShopeeApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;
        private readonly ILogger<ShopeeApiService> _logger;

        public ShopeeApiService(HttpClient httpClient, IConfiguration config, ILogger<ShopeeApiService> logger)
        {
            _httpClient = httpClient;
            _config = config;
            _logger = logger;
        }

        public async Task<IEnumerable<Product>> SearchProductsAsync(string query, int limit = 20)
        {
            try
            {
                await Task.CompletedTask;
                return Enumerable.Empty<Product>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching products on Shopee");
                return Enumerable.Empty<Product>();
            }
        }

        public async Task<Product?> GetProductByIdAsync(long itemId)
        {
            try
            {
                await Task.CompletedTask;
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting product {ItemId} from Shopee", itemId);
                return null;
            }
        }

        public async Task<IEnumerable<Category>> GetCategoriesAsync(int limit = 50)
        {
            try
            {
                await Task.CompletedTask;
                return Enumerable.Empty<Category>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting categories from Shopee");
                return Enumerable.Empty<Category>();
            }
        }
    }
}
