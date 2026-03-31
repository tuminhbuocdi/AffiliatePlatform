using Management.Domain.Entities;
using Management.Infrastructure.Repositories;

namespace Management.Application.Services
{
    public interface IProductSyncService
    {
        Task SyncProductsFromShopeeAsync(string query);
        Task<Product> SyncSingleProductAsync(long shopeeItemId);
    }

    public class ProductSyncService : IProductSyncService
    {
        public ProductSyncService(
            IProductRepository productRepository,
            ICategoryRepository categoryRepository)
        {
        }

        public async Task SyncProductsFromShopeeAsync(string query)
        {
            await Task.CompletedTask;
        }

        public async Task<Product> SyncSingleProductAsync(long shopeeItemId)
        {
            await Task.CompletedTask;
            return null;
        }
    }
}
