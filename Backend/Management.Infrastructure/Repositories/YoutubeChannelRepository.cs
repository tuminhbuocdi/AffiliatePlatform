using Dapper;
using Management.Domain.Entities;
using Management.Infrastructure.Db;
using System.Data;
using System.Linq;

namespace Management.Infrastructure.Repositories;

public class YoutubeChannelRepository : BaseRepository
{
    public YoutubeChannelRepository(IDbConnectionFactory factory) : base(factory)
    {
    }

    public async Task<IReadOnlyList<YoutubeChannelConnection>> GetByUserId(Guid userId)
    {
        using var conn = CreateConnection();
        var rows = await conn.QueryAsync<YoutubeChannelConnection>(
            "dbo.Proc_YoutubeChannelConnections_GetByUserId",
            new { UserId = userId },
            commandType: CommandType.StoredProcedure
        );
        return rows.ToList();
    }

    public async Task<YoutubeChannelConnection?> GetByUserAndChannelId(Guid userId, string channelId)
    {
        using var conn = CreateConnection();
        return await conn.QueryFirstOrDefaultAsync<YoutubeChannelConnection>(
            "dbo.Proc_YoutubeChannelConnections_GetByUserAndChannelId",
            new { UserId = userId, ChannelId = channelId },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task Upsert(YoutubeChannelConnection item)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "dbo.Proc_YoutubeChannelConnections_Upsert",
            item,
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task Revoke(Guid userId, string channelId, DateTime updatedAtUtc)
    {
        using var conn = CreateConnection();
        await conn.ExecuteAsync(
            "dbo.Proc_YoutubeChannelConnections_Revoke",
            new { UserId = userId, ChannelId = channelId, UpdatedAtUtc = updatedAtUtc },
            commandType: CommandType.StoredProcedure
        );
    }
}
