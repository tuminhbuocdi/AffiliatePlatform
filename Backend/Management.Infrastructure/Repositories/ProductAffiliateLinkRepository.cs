using Dapper;
using Management.Domain.Entities;
using Management.Infrastructure.Db;
using System.Data;

namespace Management.Infrastructure.Repositories;

public class ProductAffiliateLinkRepository
{
    private readonly IDbConnectionFactory _factory;

    public ProductAffiliateLinkRepository(IDbConnectionFactory factory)
    {
        _factory = factory;
    }

    private static DataTable ToNVarCharTvp(IEnumerable<string> values)
    {
        var dt = new DataTable();
        dt.Columns.Add("Value", typeof(string));
        foreach (var v in values)
        {
            dt.Rows.Add(v);
        }
        return dt;
    }

    public async Task<IEnumerable<ProductAffiliateLink>> GetListAsync(string? externalItemId, int limit = 200)
    {
        using var conn = _factory.Create();
        var take = Math.Max(1, Math.Min(limit, 500));

        return await conn.QueryAsync<ProductAffiliateLink>(
            "dbo.Proc_ProductAffiliateLinks_GetList",
            new
            {
                ExternalItemId = externalItemId,
                Take = take,
            },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task<ProductAffiliateLink?> GetByExternalItemIdAsync(string externalItemId)
    {
        if (string.IsNullOrWhiteSpace(externalItemId))
            throw new ArgumentException("ExternalItemId is required", nameof(externalItemId));

        using var conn = _factory.Create();
        return await conn.QueryFirstOrDefaultAsync<ProductAffiliateLink>(
            "dbo.Proc_ProductAffiliateLinks_GetByExternalItemId",
            new { ExternalItemId = externalItemId.Trim() },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task DeleteByExternalItemIdAsync(string externalItemId)
    {
        if (string.IsNullOrWhiteSpace(externalItemId))
            throw new ArgumentException("ExternalItemId is required", nameof(externalItemId));

        using var conn = _factory.Create();
        await conn.ExecuteAsync(
            "dbo.Proc_ProductAffiliateLinks_DeleteByExternalItemId",
            new { ExternalItemId = externalItemId.Trim() },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task<IReadOnlyList<(string ProductLink, string AffiliateLink)>> GetAllAffiliateMappingsAsync()
    {
        using var conn = _factory.Create();
        var rows = await conn.QueryAsync(
            "dbo.Proc_ProductAffiliateLinks_GetAllAffiliateMappings",
            commandType: CommandType.StoredProcedure
        );
        var list = new List<(string ProductLink, string AffiliateLink)>();
        foreach (var r in rows)
        {
            var productLink = (string?)r?.ProductLink;
            var affiliateLink = (string?)r?.AffiliateLink;
            if (string.IsNullOrWhiteSpace(productLink) || string.IsNullOrWhiteSpace(affiliateLink)) continue;
            list.Add((productLink.Trim(), affiliateLink.Trim()));
        }
        return list;
    }

    public async Task<IReadOnlySet<string>> GetExistingShortLinksAsync(IEnumerable<string> shortLinks)
    {
        using var conn = _factory.Create();
        var normalized = shortLinks
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var tvp = ToNVarCharTvp(normalized);
        var rows = await conn.QueryAsync<string>(
            "dbo.Proc_ProductAffiliateLinks_GetExistingShortLinks",
            new { ShortLinks = tvp.AsTableValuedParameter("dbo.Tvp_NVarChar") },
            commandType: CommandType.StoredProcedure
        );
        return new HashSet<string>(rows.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()), StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<ProductAffiliateLink>> GetByAffiliateLinksAsync(IEnumerable<string> affiliateLinks)
    {
        using var conn = _factory.Create();
        var normalized = (affiliateLinks ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0) return Array.Empty<ProductAffiliateLink>();

        var tvp = ToNVarCharTvp(normalized);
        var rows = await conn.QueryAsync<ProductAffiliateLink>(
            "dbo.Proc_ProductAffiliateLinks_GetByAffiliateLinks",
            new { Links = tvp.AsTableValuedParameter("dbo.Tvp_NVarChar") },
            commandType: CommandType.StoredProcedure
        );
        return rows.ToList();
    }

    public async Task UpsertAsync(ProductAffiliateLink model)
    {
        if (string.IsNullOrWhiteSpace(model.ExternalItemId))
            throw new ArgumentException("ExternalItemId is required", nameof(model.ExternalItemId));
        if (string.IsNullOrWhiteSpace(model.ProductLink))
            throw new ArgumentException("ProductLink is required", nameof(model.ProductLink));

        using var conn = _factory.Create();

        var now = DateTime.UtcNow;
        model.CreatedAt = model.CreatedAt == default ? now : model.CreatedAt;
        model.UpdatedAt = now;

        await conn.ExecuteAsync(
            "dbo.Proc_ProductAffiliateLinks_Upsert",
            new
            {
                model.ExternalItemId,
                model.ProductLink,
                model.SubId1,
                model.SubId2,
                model.SubId3,
                model.SubId4,
                model.SubId5,
                model.AffiliateLink,
                CreatedAt = model.CreatedAt,
                UpdatedAt = model.UpdatedAt,
            },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task<IEnumerable<ProductAffiliateLink>> GetForExportAsync(string? search, string? filter)
    {
        using var conn = _factory.Create();

        return await conn.QueryAsync<ProductAffiliateLink>(
            "dbo.Proc_ProductAffiliateLinks_GetForExport",
            new { Search = search, Filter = filter },
            commandType: CommandType.StoredProcedure
        );
    }
}
