using Management.Domain.Entities;
using Management.Infrastructure.Db;
using Dapper;
using System.Data;

namespace Management.Infrastructure.Repositories
{
    public class AffiliateClickStats
    {
        public int TotalClicks { get; set; }
        public int UniqueProducts { get; set; }
        public int RecentClicks { get; set; }
    }

    public interface IAffiliateClickRepository
    {
        Task<AffiliateClick> CreateAsync(AffiliateClick click);
        Task<IEnumerable<AffiliateClick>> GetByUserIdAsync(Guid userId);
        Task<IEnumerable<AffiliateClick>> GetByProductIdAsync(long productId);
        Task<AffiliateClickStats> GetStatsByUserIdAsync(Guid userId, DateTime recentSinceUtc);
        Task<bool> MarkAsProcessedAsync(long clickId);
    }

    public class AffiliateClickRepository : IAffiliateClickRepository
    {
        private readonly IDbConnectionFactory _dbConnectionFactory;

        public AffiliateClickRepository(IDbConnectionFactory dbConnectionFactory)
        {
            _dbConnectionFactory = dbConnectionFactory;
        }

        public async Task<AffiliateClick> CreateAsync(AffiliateClick click)
        {
            using var connection = _dbConnectionFactory.Create();
            return await connection.QuerySingleAsync<AffiliateClick>(
                "dbo.Proc_AffiliateClicks_Create",
                new
                {
                    click.UserId,
                    click.ProductId,
                    click.ClickTime,
                    click.IPAddress,
                    click.UserAgent,
                    click.IsProcessed,
                },
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task<IEnumerable<AffiliateClick>> GetByUserIdAsync(Guid userId)
        {
            using var connection = _dbConnectionFactory.Create();
            return await connection.QueryAsync<AffiliateClick>(
                "dbo.Proc_AffiliateClicks_GetByUserId",
                new { UserId = userId },
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task<IEnumerable<AffiliateClick>> GetByProductIdAsync(long productId)
        {
            using var connection = _dbConnectionFactory.Create();
            return await connection.QueryAsync<AffiliateClick>(
                "dbo.Proc_AffiliateClicks_GetByProductId",
                new { ProductId = productId },
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task<AffiliateClickStats> GetStatsByUserIdAsync(Guid userId, DateTime recentSinceUtc)
        {
            using var connection = _dbConnectionFactory.Create();
            return await connection.QuerySingleAsync<AffiliateClickStats>(
                "dbo.Proc_AffiliateClicks_GetStatsByUserId",
                new { UserId = userId, RecentSinceUtc = recentSinceUtc },
                commandType: CommandType.StoredProcedure
            );
        }

        public async Task<bool> MarkAsProcessedAsync(long clickId)
        {
            using var connection = _dbConnectionFactory.Create();
            var affected = await connection.ExecuteScalarAsync<int>(
                "dbo.Proc_AffiliateClicks_MarkAsProcessed",
                new { Id = clickId },
                commandType: CommandType.StoredProcedure
            );
            return affected > 0;
        }
    }
}
