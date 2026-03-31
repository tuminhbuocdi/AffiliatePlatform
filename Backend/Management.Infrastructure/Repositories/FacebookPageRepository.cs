using Dapper;
using Management.Domain.Entities;
using Management.Infrastructure.Db;
using System.Data;
using System.Linq;

namespace Management.Infrastructure.Repositories;

public class FacebookPageRepository : BaseRepository
{
    public FacebookPageRepository(IDbConnectionFactory factory) : base(factory)
    {
    }

    public async Task<IReadOnlyList<FacebookPageConnection>> GetByUserId(Guid userId)
    {
        using var conn = CreateConnection();
        var rows = await conn.QueryAsync<FacebookPageConnection>(
            "dbo.Proc_FacebookPageConnections_GetByUserId",
            new { UserId = userId },
            commandType: CommandType.StoredProcedure
        );
        return rows.ToList();
    }

    public async Task<FacebookPageConnection?> GetAnyActiveByUserId(Guid userId)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<FacebookPageConnection>(
            "dbo.Proc_FacebookPageConnections_GetAnyActiveByUserId",
            new { UserId = userId },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task<FacebookPageConnection?> GetByUserAndPageId(Guid userId, string pageId)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<FacebookPageConnection>(
            "dbo.Proc_FacebookPageConnections_GetByUserAndPageId",
            new { UserId = userId, PageId = pageId },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task Upsert(FacebookPageConnection item)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "dbo.Proc_FacebookPageConnections_Upsert",
            item,
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task Revoke(Guid userId, string pageId, DateTime updatedAtUtc)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "dbo.Proc_FacebookPageConnections_Revoke",
            new { UserId = userId, PageId = pageId, UpdatedAtUtc = updatedAtUtc },
            commandType: CommandType.StoredProcedure
        );
    }
}
