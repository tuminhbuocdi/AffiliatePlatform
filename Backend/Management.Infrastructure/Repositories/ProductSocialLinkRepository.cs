using Dapper;
using Management.Domain.Entities;
using Management.Infrastructure.Db;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Management.Infrastructure.Repositories;

public class ProductSocialLinkRepository : BaseRepository
{
    public ProductSocialLinkRepository(IDbConnectionFactory factory) : base(factory)
    {
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

    private static DataTable ToProductSocialLinkTvp(IEnumerable<ProductSocialLink> items)
    {
        var dt = new DataTable();
        dt.Columns.Add("ProductLink", typeof(string));
        dt.Columns.Add("AffiliateLink", typeof(string));
        dt.Columns.Add("SocialLink", typeof(string));
        dt.Columns.Add("CreatedAt", typeof(DateTime));

        foreach (var it in items)
        {
            var createdAt = it.CreatedAt == default ? DateTime.UtcNow : it.CreatedAt;
            dt.Rows.Add(it.ProductLink, it.AffiliateLink, it.SocialLink, createdAt);
        }

        return dt;
    }

    public async Task<HashSet<string>> GetExistingSocialLinksAsync(IEnumerable<string> socialLinks)
    {
        var list = (socialLinks ?? Array.Empty<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (list.Count == 0) return result;

        using var conn = CreateConnection();
        var tvp = ToNVarCharTvp(list);
        var rows = await conn.QueryAsync<string>(
            "dbo.Proc_ProductSocialLinks_GetExistingSocialLinks",
            new { Links = tvp.AsTableValuedParameter("dbo.Tvp_NVarChar") },
            commandType: CommandType.StoredProcedure
        );
        foreach (var r in rows)
        {
            if (!string.IsNullOrWhiteSpace(r)) result.Add(r.Trim());
        }

        return result;
    }

    public async Task<int> DeleteBySocialLinkAsync(string socialLink)
    {
        var normalized = (socialLink ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return 0;

        using var conn = CreateConnection();
        return await conn.ExecuteScalarAsync<int>(
            "dbo.Proc_ProductSocialLinks_DeleteBySocialLink",
            new { SocialLink = normalized },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task<int> BulkInsertIgnoreDuplicatesAsync(IEnumerable<ProductSocialLink> items)
    {
        var list = (items ?? Array.Empty<ProductSocialLink>()).ToList();
        if (list.Count == 0) return 0;

        using var conn = CreateConnection();

        var tvp = ToProductSocialLinkTvp(list);
        var inserted = await conn.ExecuteScalarAsync<int>(
            "dbo.Proc_ProductSocialLinks_BulkInsertIgnoreDuplicates",
            new { Items = tvp.AsTableValuedParameter("dbo.Tvp_ProductSocialLink") },
            commandType: CommandType.StoredProcedure
        );

        return inserted;
    }
}
