using Management.Domain.Entities;
using Management.Infrastructure.Db;
using Dapper;
using System.Data;

namespace Management.Infrastructure.Repositories
{
    public class ProductAffiliateLinkPickItem
    {
        public string ExternalItemId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? ImageKey { get; set; }
        public string? AffiliateLink { get; set; }
        public bool HasSocialLinks { get; set; }
    }

    public interface IProductRepository
    {
        Task<IEnumerable<Product>> GetAllAsync();
        Task<IEnumerable<Product>> GetLatestAsync(string? search, int limit = 50, string? affiliateFilter = null);
        Task<(IEnumerable<ProductAffiliateLinkPickItem> Items, int Total)> GetAffiliateLinkPickerPageAsync(string? search, int page, int pageSize);
        Task<Product?> GetByIdAsync(long id);
        Task<Product?> GetByProductLinkAsync(string productLink);
        Task<Product> UpsertByShopeeItemIdAsync(Product product);
        Task<Product> UpsertByExternalAsync(Product product);
        Task<IEnumerable<Product>> GetByCategoryAsync(int categoryId);
        Task<IEnumerable<Product>> SearchAsync(string query);
        Task<Product> CreateAsync(Product product);
        Task<Product> UpdateAsync(Product product);
        Task<bool> DeleteAsync(long id);
    }

    public class ProductRepository : IProductRepository
    {
        private readonly IDbConnectionFactory _dbConnectionFactory;

        private const string ProductCoreSelect = @"
    p.Id,
    p.Platform,
    p.ExternalItemId,
    p.ShopeeItemId,
    p.Name,
    p.Description,
    p.Slug,
    p.ImageUrl,
    p.IsProcessed,
    p.LastSynced,
    p.CreatedAt,
    p.UpdatedAt";

        private const string ProductDetailSelect = @"
    d.ItemId AS ItemId,
    d.ProductLink AS ProductLink,
    d.MaxCommissionRateRaw AS MaxCommissionRateRaw,
    d.SellerCommissionRateRaw AS SellerCommissionRateRaw,
    d.DefaultCommissionRateRaw AS DefaultCommissionRateRaw,
    d.ShopId AS ShopId,
    d.ImagesJson AS ImagesJson,
    d.VideoInfoListJson AS VideoInfoListJson,
    d.TierVariationsJson AS TierVariationsJson,
    d.ItemRatingJson AS ItemRatingJson,
    d.OfferCardType AS OfferCardType,
    d.Currency AS Currency,
    d.Stock AS Stock,
    d.Status AS Status,
    d.Ctime AS Ctime,
    d.SoldRaw AS SoldRaw,
    d.SoldText AS SoldText,
    d.CatId AS CatId,
    d.ItemStatus AS ItemStatus,
    d.PriceRaw AS PriceRaw,
    d.PriceMinRaw AS PriceMinRaw,
    d.PriceMaxRaw AS PriceMaxRaw,
    d.PriceMinBeforeDiscountRaw AS PriceMinBeforeDiscountRaw,
    d.PriceMaxBeforeDiscountRaw AS PriceMaxBeforeDiscountRaw,
    d.PriceBeforeDiscountRaw AS PriceBeforeDiscountRaw,
    d.Discount AS Discount,
    d.ItemType AS ItemType,
    d.ShopeeVerified AS ShopeeVerified,
    d.IsOfficialShop AS IsOfficialShop,
    d.ShopLocation AS ShopLocation,
    d.CanUseCod AS CanUseCod,
    d.IsOnFlashSale AS IsOnFlashSale,
    d.ShopName AS ShopName,
    d.ShopRating AS ShopRating";

        public ProductRepository(IDbConnectionFactory dbConnectionFactory)
        {
            _dbConnectionFactory = dbConnectionFactory;
        }

        public async Task<IEnumerable<Product>> GetAllAsync()
        {
            using var connection = _dbConnectionFactory.Create();
            var productDict = new Dictionary<long, Product>();

            await connection.QueryAsync<Product, Category, Product>(
                "dbo.Proc_Product_GetAllWithCategories",
                (product, category) =>
                {
                    if (!productDict.TryGetValue(product.Id, out var productEntry))
                    {
                        productEntry = product;
                        productDict[product.Id] = productEntry;
                    }

                    return productEntry;
                },
                splitOn: "Id",
                commandType: CommandType.StoredProcedure
            );

            return productDict.Values;
        }

        public async Task<IEnumerable<Product>> GetLatestAsync(string? search, int limit = 50, string? affiliateFilter = null)
        {
            using var connection = _dbConnectionFactory.Create();
            var take = Math.Max(1, Math.Min(limit, 200));

            return await connection.QueryAsync<Product>(
                "dbo.Proc_Product_GetLatest",
                new { Search = search, Take = take, AffiliateFilter = affiliateFilter },
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task<(IEnumerable<ProductAffiliateLinkPickItem> Items, int Total)> GetAffiliateLinkPickerPageAsync(string? search, int page, int pageSize)
        {
            using var connection = _dbConnectionFactory.Create();
            var take = Math.Max(1, Math.Min(pageSize, 50));
            var p = Math.Max(1, page);

            using var multi = await connection.QueryMultipleAsync(
                "dbo.Proc_ProductAffiliateLinkPicker_GetPage",
                new { Search = search, Page = p, PageSize = take },
                commandType: CommandType.StoredProcedure
            );

            var total = await multi.ReadFirstAsync<int>();
            var items = await multi.ReadAsync<ProductAffiliateLinkPickItem>();
            return (items, total);
        }

        public async Task<Product?> GetByProductLinkAsync(string productLink)
        {
            using var connection = _dbConnectionFactory.Create();
            return await connection.QueryFirstOrDefaultAsync<Product>(
                "dbo.Proc_Product_GetByProductLink",
                new { ProductLink = productLink },
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task<Product?> GetByIdAsync(long id)
        {
            using var connection = _dbConnectionFactory.Create();
            return await connection.QueryFirstOrDefaultAsync<Product>(
                "dbo.Proc_Product_GetById",
                new { Id = id },
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task<Product> UpsertByShopeeItemIdAsync(Product product)
        {
            product.Platform = string.IsNullOrWhiteSpace(product.Platform) ? "Shopee" : product.Platform;
            product.ExternalItemId = string.IsNullOrWhiteSpace(product.ExternalItemId)
                ? product.ShopeeItemId
                : product.ExternalItemId;

            using var connection = _dbConnectionFactory.Create();
            var existingId = await connection.QueryFirstOrDefaultAsync<long?>(
                "dbo.Proc_Product_FindIdByShopeeItemId",
                new { product.ShopeeItemId },
                commandType: CommandType.StoredProcedure
            );

            if (existingId == null)
            {
                product.CreatedAt = product.CreatedAt == default ? DateTime.UtcNow : product.CreatedAt;
                return await CreateAsync(product);
            }

            product.Id = existingId.Value;
            return await UpdateAsync(product);
        }

        public async Task<Product> UpsertByExternalAsync(Product product)
        {
            if (string.IsNullOrWhiteSpace(product.Platform))
                throw new ArgumentException("Platform is required", nameof(product.Platform));
            if (string.IsNullOrWhiteSpace(product.ExternalItemId))
                throw new ArgumentException("ExternalItemId is required", nameof(product.ExternalItemId));

            using var connection = _dbConnectionFactory.Create();
            var existingId = await connection.QueryFirstOrDefaultAsync<long?>(
                "dbo.Proc_Product_FindIdByPlatformExternal",
                new { product.Platform, product.ExternalItemId },
                commandType: CommandType.StoredProcedure
            );

            if (existingId == null)
            {
                product.CreatedAt = product.CreatedAt == default ? DateTime.UtcNow : product.CreatedAt;
                return await CreateAsync(product);
            }

            product.Id = existingId.Value;
            return await UpdateAsync(product);
        }

        public async Task<IEnumerable<Product>> GetByCategoryAsync(int categoryId)
        {
            using var connection = _dbConnectionFactory.Create();

            return await connection.QueryAsync<Product>(
                "dbo.Proc_Product_GetByCategory",
                new { CategoryId = categoryId },
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task<IEnumerable<Product>> SearchAsync(string query)
        {
            using var connection = _dbConnectionFactory.Create();

            return await connection.QueryAsync<Product>(
                "dbo.Proc_Product_Search",
                new { Query = query },
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task<Product> CreateAsync(Product product)
        {
            using var connection = _dbConnectionFactory.Create();
            connection.Open();
            using var tx = connection.BeginTransaction();
            try
            {
                product.CreatedAt = product.CreatedAt == default ? DateTime.UtcNow : product.CreatedAt;
                product.UpdatedAt = product.UpdatedAt == default ? product.CreatedAt : product.UpdatedAt;

                product.Id = await connection.ExecuteScalarAsync<long>(
                    "dbo.Proc_Product_CreateCore",
                    new
                    {
                        product.Platform,
                        product.ExternalItemId,
                        product.ShopeeItemId,
                        product.Name,
                        product.Description,
                        product.Slug,
                        product.ImageUrl,
                        product.IsProcessed,
                        product.LastSynced,
                        product.CreatedAt,
                        product.UpdatedAt,
                    },
                    tx,
                    commandType: CommandType.StoredProcedure
                );

                await UpsertDetailsAsync(connection, tx, product);
                tx.Commit();
                return await GetByIdAsync(product.Id) ?? product;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        public async Task<Product> UpdateAsync(Product product)
        {
            using var connection = _dbConnectionFactory.Create();
            connection.Open();
            using var tx = connection.BeginTransaction();
            try
            {
                product.UpdatedAt = product.UpdatedAt == default ? DateTime.UtcNow : product.UpdatedAt;

                await connection.ExecuteAsync(
                    "dbo.Proc_Product_UpdateCore",
                    new
                    {
                        product.Id,
                        product.Platform,
                        product.ExternalItemId,
                        product.ShopeeItemId,
                        product.Name,
                        product.Description,
                        product.Slug,
                        product.ImageUrl,
                        product.IsProcessed,
                        product.LastSynced,
                        product.UpdatedAt,
                    },
                    tx,
                    commandType: CommandType.StoredProcedure
                );

                await UpsertDetailsAsync(connection, tx, product);
                tx.Commit();
                return await GetByIdAsync(product.Id) ?? product;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }

        private static Task UpsertDetailsAsync(IDbConnection connection, IDbTransaction tx, Product product)
        {
            return connection.ExecuteAsync(
                "dbo.Proc_ProductDetails_Upsert",
                new
                {
                    ProductId = product.Id,
                    ItemId = product.ExternalItemId,
                    product.ProductLink,
                    product.MaxCommissionRateRaw,
                    product.SellerCommissionRateRaw,
                    product.DefaultCommissionRateRaw,
                    product.ShopId,
                    product.ImagesJson,
                    product.VideoInfoListJson,
                    product.TierVariationsJson,
                    product.ItemRatingJson,
                    product.OfferCardType,
                    product.Currency,
                    product.Stock,
                    product.Status,
                    product.Ctime,
                    product.SoldRaw,
                    product.SoldText,
                    product.CatId,
                    product.ItemStatus,
                    product.PriceRaw,
                    product.PriceMinRaw,
                    product.PriceMaxRaw,
                    product.PriceMinBeforeDiscountRaw,
                    product.PriceMaxBeforeDiscountRaw,
                    product.PriceBeforeDiscountRaw,
                    product.Discount,
                    product.ItemType,
                    product.ShopeeVerified,
                    product.IsOfficialShop,
                    product.ShopLocation,
                    product.CanUseCod,
                    product.IsOnFlashSale,
                    product.ShopName,
                    product.ShopRating,
                },
                tx,
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task<bool> DeleteAsync(long id)
        {
            using var connection = _dbConnectionFactory.Create();
            var affected = await connection.ExecuteScalarAsync<int>(
                "dbo.Proc_Product_Delete",
                new { Id = id },
                commandType: CommandType.StoredProcedure
            );
            return affected > 0;
        }
    }
}
