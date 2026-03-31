using Dapper;
using Management.Infrastructure.Db;
using System.Data;

namespace Management.Infrastructure.Repositories;

public sealed class AssTemplateRepository
{
    private readonly IDbConnectionFactory _db;

    public AssTemplateRepository(IDbConnectionFactory db)
    {
        _db = db;
    }

    public sealed class AssTemplateRow
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Tags { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }

    public async Task<IReadOnlyList<AssTemplateRow>> GetAllAsync(bool? isActive = null, string? search = null)
    {
        using var conn = _db.Create();

        var sql = "SELECT TOP 500 * FROM dbo.AssTemplates WHERE 1=1";
        if (isActive != null) sql += " AND IsActive = @IsActive";
        if (!string.IsNullOrWhiteSpace(search)) sql += " AND (Name LIKE @Q OR Tags LIKE @Q)";
        sql += " ORDER BY UpdatedAtUtc DESC";

        var rows = await conn.QueryAsync<AssTemplateRow>(sql, new
        {
            IsActive = isActive,
            Q = string.IsNullOrWhiteSpace(search) ? null : ("%" + search.Trim() + "%"),
        });

        return rows.ToList();
    }

    public async Task<AssTemplateRow?> GetByIdAsync(Guid id)
    {
        using var conn = _db.Create();
        return await conn.QueryFirstOrDefaultAsync<AssTemplateRow>(
            "SELECT * FROM dbo.AssTemplates WHERE Id = @Id",
            new { Id = id }
        );
    }

    public async Task<Guid> CreateAsync(string name, string? tags, string content, bool isActive)
    {
        using var conn = _db.Create();
        var now = DateTime.UtcNow;
        var id = Guid.NewGuid();

        await conn.ExecuteAsync(
            @"INSERT INTO dbo.AssTemplates (Id, Name, Tags, Content, IsActive, CreatedAtUtc, UpdatedAtUtc)
              VALUES (@Id, @Name, @Tags, @Content, @IsActive, @CreatedAtUtc, @UpdatedAtUtc)",
            new
            {
                Id = id,
                Name = name,
                Tags = string.IsNullOrWhiteSpace(tags) ? null : tags.Trim(),
                Content = content,
                IsActive = isActive,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            }
        );

        return id;
    }

    public async Task<bool> UpdateAsync(Guid id, string name, string? tags, string content, bool isActive)
    {
        using var conn = _db.Create();
        var n = await conn.ExecuteAsync(
            @"UPDATE dbo.AssTemplates
              SET Name = @Name,
                  Tags = @Tags,
                  Content = @Content,
                  IsActive = @IsActive,
                  UpdatedAtUtc = @UpdatedAtUtc
              WHERE Id = @Id",
            new
            {
                Id = id,
                Name = name,
                Tags = string.IsNullOrWhiteSpace(tags) ? null : tags.Trim(),
                Content = content,
                IsActive = isActive,
                UpdatedAtUtc = DateTime.UtcNow,
            }
        );
        return n > 0;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        using var conn = _db.Create();
        var n = await conn.ExecuteAsync(
            "DELETE FROM dbo.AssTemplates WHERE Id = @Id",
            new { Id = id }
        );
        return n > 0;
    }
}
