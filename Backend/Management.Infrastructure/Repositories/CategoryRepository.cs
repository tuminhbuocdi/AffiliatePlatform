using Management.Domain.Entities;
using Management.Infrastructure.Db;
using Dapper;
using System.Data;

namespace Management.Infrastructure.Repositories
{
    public interface ICategoryRepository
    {
        Task<IEnumerable<Category>> GetAllAsync();
        Task<Category?> GetByIdAsync(int id);
        Task<Category?> GetBySlugAsync(string slug);
        Task<IEnumerable<Category>> GetRootCategoriesAsync();
        Task<IEnumerable<Category>> GetChildCategoriesAsync(int parentId);
        Task<Category> CreateAsync(Category category);
        Task<Category> UpdateAsync(Category category);
        Task<bool> DeleteAsync(int id);
    }

    public class CategoryRepository : ICategoryRepository
    {
        private readonly IDbConnectionFactory _dbConnectionFactory;

        public CategoryRepository(IDbConnectionFactory dbConnectionFactory)
        {
            _dbConnectionFactory = dbConnectionFactory;
        }

        public async Task<IEnumerable<Category>> GetAllAsync()
        {
            using var connection = _dbConnectionFactory.Create();
            return await connection.QueryAsync<Category>(
                "dbo.Proc_Categories_GetAll",
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task<Category?> GetByIdAsync(int id)
        {
            using var connection = _dbConnectionFactory.Create();
            return await connection.QueryFirstOrDefaultAsync<Category>(
                "dbo.Proc_Categories_GetById",
                new { Id = id },
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task<Category?> GetBySlugAsync(string slug)
        {
            using var connection = _dbConnectionFactory.Create();
            return await connection.QueryFirstOrDefaultAsync<Category>(
                "dbo.Proc_Categories_GetBySlug",
                new { Slug = slug },
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task<IEnumerable<Category>> GetRootCategoriesAsync()
        {
            using var connection = _dbConnectionFactory.Create();
            return await connection.QueryAsync<Category>(
                "dbo.Proc_Categories_GetRoot",
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task<IEnumerable<Category>> GetChildCategoriesAsync(int parentId)
        {
            using var connection = _dbConnectionFactory.Create();
            return await connection.QueryAsync<Category>(
                "dbo.Proc_Categories_GetChildren",
                new { ParentId = parentId },
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task<Category> CreateAsync(Category category)
        {
            using var connection = _dbConnectionFactory.Create();
            return await connection.QuerySingleAsync<Category>(
                "dbo.Proc_Categories_Create",
                category,
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task<Category> UpdateAsync(Category category)
        {
            using var connection = _dbConnectionFactory.Create();
            return await connection.QuerySingleAsync<Category>(
                "dbo.Proc_Categories_Update",
                category,
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var connection = _dbConnectionFactory.Create();
            var affected = await connection.ExecuteScalarAsync<int>(
                "dbo.Proc_Categories_Delete",
                new { Id = id },
                commandType: CommandType.StoredProcedure
            );
            return affected > 0;
        }
    }
}
