using Dapper;
using Management.Infrastructure.Db;

namespace Management.Infrastructure.Repositories;

public sealed class GeneratedSubtitleRepository
{
    private readonly IDbConnectionFactory _db;

    public GeneratedSubtitleRepository(IDbConnectionFactory db)
    {
        _db = db;
    }

    public sealed class GeneratedSubtitleRow
    {
        public Guid Id { get; set; }
        public string SourceFileName { get; set; } = string.Empty;
        public string AssText { get; set; } = string.Empty;
        public Guid? AssTemplateId { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    public async Task<Guid> CreateAsync(string sourceFileName, string assText, Guid? assTemplateId)
    {
        using var conn = _db.Create();
        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await conn.ExecuteAsync(
            @"INSERT INTO dbo.GeneratedSubtitles (Id, SourceFileName, AssText, AssTemplateId, CreatedAtUtc, UpdatedAtUtc)
              VALUES (@Id, @SourceFileName, @AssText, @AssTemplateId, @CreatedAtUtc, @UpdatedAtUtc)",
            new
            {
                Id = id,
                SourceFileName = sourceFileName,
                AssText = assText,
                AssTemplateId = assTemplateId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            }
        );

        return id;
    }

    public async Task<GeneratedSubtitleRow?> GetByIdAsync(Guid id)
    {
        using var conn = _db.Create();
        return await conn.QueryFirstOrDefaultAsync<GeneratedSubtitleRow>(
            "SELECT * FROM dbo.GeneratedSubtitles WHERE Id = @Id",
            new { Id = id }
        );
    }

    public async Task<IReadOnlyList<GeneratedSubtitleRow>> GetLatestAsync(int limit = 50)
    {
        using var conn = _db.Create();
        var n = Math.Clamp(limit, 1, 200);
        var rows = await conn.QueryAsync<GeneratedSubtitleRow>(
            $"SELECT TOP ({n}) * FROM dbo.GeneratedSubtitles ORDER BY CreatedAtUtc DESC"
        );
        return rows.ToList();
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        using var conn = _db.Create();
        var n = await conn.ExecuteAsync(
            "DELETE FROM dbo.GeneratedSubtitles WHERE Id = @Id",
            new { Id = id }
        );
        return n > 0;
    }

    public async Task<bool> UpdateSourceFileNameAsync(Guid id, string sourceFileName)
    {
        using var conn = _db.Create();
        var now = DateTime.UtcNow;
        var n = await conn.ExecuteAsync(
            @"UPDATE dbo.GeneratedSubtitles
              SET SourceFileName = @SourceFileName,
                  UpdatedAtUtc = @UpdatedAtUtc
              WHERE Id = @Id",
            new
            {
                Id = id,
                SourceFileName = sourceFileName,
                UpdatedAtUtc = now,
            }
        );
        return n > 0;
    }
}
