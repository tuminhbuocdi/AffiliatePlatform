using Dapper;
using Management.Infrastructure.Db;
using System.Data;

namespace Management.Infrastructure.Repositories;

public sealed class ScheduledPostRepository
{
    private readonly IDbConnectionFactory _db;

    public ScheduledPostRepository(IDbConnectionFactory db)
    {
        _db = db;
    }

    public sealed class ScheduledPostRow
    {
        public Guid Id { get; set; }
        public long? CreatedBy { get; set; }
        public string? PageId { get; set; }
        public string Caption { get; set; } = string.Empty;
        public DateTime ScheduledAtLocal { get; set; }
        public DateTime ScheduledAtUtc { get; set; }
        public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";
        public string TargetsJson { get; set; } = "{}";
        public string Status { get; set; } = "Scheduled";
        public DateTime? RemindedAtUtc { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public DateTime? DoneAtUtc { get; set; }
        public DateTime? CanceledAtUtc { get; set; }
    }

    public sealed class ScheduledPostAssetRow
    {
        public Guid Id { get; set; }
        public Guid ScheduledPostId { get; set; }
        public string AssetType { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }

    public sealed record CreateScheduledPost(
        long? CreatedBy,
        string? PageId,
        string Caption,
        DateTime ScheduledAtLocal,
        DateTime ScheduledAtUtc,
        string Timezone,
        string TargetsJson,
        IReadOnlyList<(string AssetType, string Url, int SortOrder)> Assets
    );

    public async Task<Guid> CreateAsync(CreateScheduledPost input)
    {
        using var conn = _db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var now = DateTime.UtcNow;
        var id = Guid.NewGuid();

        await conn.ExecuteAsync(
            @"INSERT INTO dbo.ScheduledPosts
                (Id, CreatedBy, PageId, Caption, ScheduledAtLocal, ScheduledAtUtc, Timezone, TargetsJson, Status, RemindedAtUtc, CreatedAtUtc, UpdatedAtUtc, DoneAtUtc, CanceledAtUtc)
              VALUES
                (@Id, @CreatedBy, @PageId, @Caption, @ScheduledAtLocal, @ScheduledAtUtc, @Timezone, @TargetsJson, @Status, NULL, @CreatedAtUtc, @UpdatedAtUtc, NULL, NULL)",
            new
            {
                Id = id,
                input.CreatedBy,
                input.PageId,
                input.Caption,
                input.ScheduledAtLocal,
                input.ScheduledAtUtc,
                Timezone = string.IsNullOrWhiteSpace(input.Timezone) ? "Asia/Ho_Chi_Minh" : input.Timezone,
                TargetsJson = string.IsNullOrWhiteSpace(input.TargetsJson) ? "{}" : input.TargetsJson,
                Status = "Scheduled",
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            },
            tx
        );

        if (input.Assets != null && input.Assets.Count > 0)
        {
            foreach (var a in input.Assets)
            {
                await conn.ExecuteAsync(
                    @"INSERT INTO dbo.ScheduledPostAssets (Id, ScheduledPostId, AssetType, Url, SortOrder)
                      VALUES (@Id, @ScheduledPostId, @AssetType, @Url, @SortOrder)",
                    new
                    {
                        Id = Guid.NewGuid(),
                        ScheduledPostId = id,
                        AssetType = (a.AssetType ?? string.Empty).Trim().ToLowerInvariant(),
                        Url = (a.Url ?? string.Empty).Trim(),
                        SortOrder = a.SortOrder,
                    },
                    tx
                );
            }
        }

        tx.Commit();
        return id;
    }

    public async Task<(IReadOnlyList<ScheduledPostRow> Posts, IReadOnlyList<ScheduledPostAssetRow> Assets)> GetByIdsAsync(IReadOnlyList<Guid> ids)
    {
        if (ids == null || ids.Count == 0) return (Array.Empty<ScheduledPostRow>(), Array.Empty<ScheduledPostAssetRow>());

        using var conn = _db.Create();
        var posts = (await conn.QueryAsync<ScheduledPostRow>(
            @"SELECT * FROM dbo.ScheduledPosts WHERE Id IN @Ids",
            new { Ids = ids.ToArray() }
        )).ToList();

        var assets = (await conn.QueryAsync<ScheduledPostAssetRow>(
            @"SELECT * FROM dbo.ScheduledPostAssets WHERE ScheduledPostId IN @Ids ORDER BY ScheduledPostId, SortOrder",
            new { Ids = ids.ToArray() }
        )).ToList();

        return (posts, assets);
    }

    public async Task<(IReadOnlyList<ScheduledPostRow> Posts, IReadOnlyList<ScheduledPostAssetRow> Assets)> GetRangeAsync(DateTime fromUtc, DateTime toUtc, string? status = null)
    {
        using var conn = _db.Create();

        var sql = "SELECT * FROM dbo.ScheduledPosts WHERE ScheduledAtUtc >= @FromUtc AND ScheduledAtUtc <= @ToUtc";
        if (!string.IsNullOrWhiteSpace(status)) sql += " AND Status = @Status";
        sql += " ORDER BY ScheduledAtUtc";

        var posts = (await conn.QueryAsync<ScheduledPostRow>(sql, new { FromUtc = fromUtc, ToUtc = toUtc, Status = status })).ToList();
        var ids = posts.Select(x => x.Id).ToArray();

        var assets = ids.Length == 0
            ? new List<ScheduledPostAssetRow>()
            : (await conn.QueryAsync<ScheduledPostAssetRow>(
                @"SELECT * FROM dbo.ScheduledPostAssets WHERE ScheduledPostId IN @Ids ORDER BY ScheduledPostId, SortOrder",
                new { Ids = ids }
            )).ToList();

        return (posts, assets);
    }

    public async Task<(IReadOnlyList<ScheduledPostRow> DuePosts, IReadOnlyList<ScheduledPostAssetRow> Assets)> GetDueAndMarkAsync(DateTime nowUtc)
    {
        using var conn = _db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();

        var due = (await conn.QueryAsync<ScheduledPostRow>(
            @"SELECT TOP 50 *
              FROM dbo.ScheduledPosts
              WHERE Status IN ('Scheduled')
                AND ScheduledAtUtc <= @NowUtc
              ORDER BY ScheduledAtUtc",
            new { NowUtc = nowUtc },
            tx
        )).ToList();

        if (due.Count > 0)
        {
            var ids = due.Select(x => x.Id).ToArray();
            await conn.ExecuteAsync(
                @"UPDATE dbo.ScheduledPosts
                  SET Status = 'Due', RemindedAtUtc = ISNULL(RemindedAtUtc, @NowUtc), UpdatedAtUtc = @NowUtc
                  WHERE Id IN @Ids AND Status = 'Scheduled'",
                new { NowUtc = nowUtc, Ids = ids },
                tx
            );
        }

        tx.Commit();

        var assets = due.Count == 0
            ? new List<ScheduledPostAssetRow>()
            : (await conn.QueryAsync<ScheduledPostAssetRow>(
                @"SELECT * FROM dbo.ScheduledPostAssets WHERE ScheduledPostId IN @Ids ORDER BY ScheduledPostId, SortOrder",
                new { Ids = due.Select(x => x.Id).ToArray() }
            )).ToList();

        return (due, assets);
    }

    public async Task<bool> MarkDoneAsync(Guid id, DateTime nowUtc)
    {
        using var conn = _db.Create();
        var n = await conn.ExecuteAsync(
            @"UPDATE dbo.ScheduledPosts
              SET Status = 'Done', DoneAtUtc = ISNULL(DoneAtUtc, @NowUtc), UpdatedAtUtc = @NowUtc
              WHERE Id = @Id AND Status IN ('Scheduled','Due')",
            new { Id = id, NowUtc = nowUtc }
        );
        return n > 0;
    }

    public async Task<bool> CancelAsync(Guid id, DateTime nowUtc)
    {
        using var conn = _db.Create();
        var n = await conn.ExecuteAsync(
            @"UPDATE dbo.ScheduledPosts
              SET Status = 'Canceled', CanceledAtUtc = ISNULL(CanceledAtUtc, @NowUtc), UpdatedAtUtc = @NowUtc
              WHERE Id = @Id AND Status IN ('Scheduled','Due')",
            new { Id = id, NowUtc = nowUtc }
        );
        return n > 0;
    }
}
