using System.Data;
using Dapper;
using Management.Domain.Entities;
using Management.Infrastructure.Db;

namespace Management.Infrastructure.Repositories;

public sealed class MusicRepository
{
    private readonly IDbConnectionFactory _db;

    public MusicRepository(IDbConnectionFactory db)
    {
        _db = db;
    }

    public async Task<IEnumerable<MusicTopic>> GetTopicsAsync()
    {
        using var conn = _db.Create();
        return await conn.QueryAsync<MusicTopic>("dbo.Proc_MusicTopics_GetAll", commandType: CommandType.StoredProcedure);
    }

    public async Task<MusicTopic> UpsertTopicAsync(MusicTopic topic)
    {
        using var conn = _db.Create();
        return await conn.QuerySingleAsync<MusicTopic>(
            "dbo.Proc_MusicTopics_Upsert",
            new { topic.Id, topic.Name, topic.IsActive },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task<IEnumerable<MusicStyle>> GetStylesAsync()
    {
        using var conn = _db.Create();
        return await conn.QueryAsync<MusicStyle>("dbo.Proc_MusicStyles_GetAll", commandType: CommandType.StoredProcedure);
    }

    public async Task<MusicStyle> UpsertStyleAsync(MusicStyle style)
    {
        using var conn = _db.Create();
        return await conn.QuerySingleAsync<MusicStyle>(
            "dbo.Proc_MusicStyles_Upsert",
            new { style.Id, style.Name, style.IsActive },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task<(IReadOnlyList<Music> Items, int Total)> GetPageAsync(string? search, int? topicId, int? styleId, int page, int pageSize)
    {
        using var conn = _db.Create();

        using var multi = await conn.QueryMultipleAsync(
            "dbo.Proc_Musics_GetPage",
            new { Search = search, TopicId = topicId, StyleId = styleId, Page = page, PageSize = pageSize },
            commandType: CommandType.StoredProcedure
        );

        var total = await multi.ReadFirstAsync<int>();
        var items = (await multi.ReadAsync<Music>()).ToList();
        return (items, total);
    }

    public async Task<Music?> GetByIdAsync(string id)
    {
        using var conn = _db.Create();
        return await conn.QueryFirstOrDefaultAsync<Music>(
            "dbo.Proc_Musics_GetById",
            new { Id = id },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task<Music> UpsertAsync(Music music)
    {
        using var conn = _db.Create();
        return await conn.QuerySingleAsync<Music>(
            "dbo.Proc_Musics_Upsert",
            new
            {
                music.Id,
                music.IdStr,
                music.Name,
                music.Author,
                music.Album,
                music.Language,
                music.Category,
                DurationSeconds = music.DurationSeconds,
                music.AudioUrl,
                music.IsActive,
            },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task ReplaceTopicsAsync(string musicId, IReadOnlyCollection<int> topicIds)
    {
        using var conn = _db.Create();
        var tvp = new DataTable();
        tvp.Columns.Add("Value", typeof(int));
        foreach (var id in (topicIds ?? Array.Empty<int>()).Where(x => x > 0).Distinct()) tvp.Rows.Add(id);

        await conn.ExecuteAsync(
            "dbo.Proc_MusicTopicMap_Replace",
            new { MusicId = musicId, TopicIds = tvp.AsTableValuedParameter("dbo.Tvp_Int") },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task ReplaceStylesAsync(string musicId, IReadOnlyCollection<int> styleIds)
    {
        using var conn = _db.Create();
        var tvp = new DataTable();
        tvp.Columns.Add("Value", typeof(int));
        foreach (var id in (styleIds ?? Array.Empty<int>()).Where(x => x > 0).Distinct()) tvp.Rows.Add(id);

        await conn.ExecuteAsync(
            "dbo.Proc_MusicStyleMap_Replace",
            new { MusicId = musicId, StyleIds = tvp.AsTableValuedParameter("dbo.Tvp_Int") },
            commandType: CommandType.StoredProcedure
        );
    }

    public async Task<Dictionary<string, int[]>> GetTopicMapAsync(IReadOnlyCollection<string> musicIds)
    {
        var ids = (musicIds ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);

        using var conn = _db.Create();

        var tvp = new DataTable();
        tvp.Columns.Add("Value", typeof(string));
        foreach (var id in ids) tvp.Rows.Add(id);

        var rows = await conn.QueryAsync<(string MusicId, int TopicId)>(
            "dbo.Proc_MusicTopicMap_GetByMusicIds",
            new { MusicIds = tvp.AsTableValuedParameter("dbo.Tvp_NVarChar") },
            commandType: CommandType.StoredProcedure
        );

        return rows
            .GroupBy(x => x.MusicId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(x => x.TopicId).Distinct().ToArray(), StringComparer.OrdinalIgnoreCase);
    }

    public async Task<Dictionary<string, int[]>> GetStyleMapAsync(IReadOnlyCollection<string> musicIds)
    {
        var ids = (musicIds ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);

        using var conn = _db.Create();

        var tvp = new DataTable();
        tvp.Columns.Add("Value", typeof(string));
        foreach (var id in ids) tvp.Rows.Add(id);

        var rows = await conn.QueryAsync<(string MusicId, int StyleId)>(
            "dbo.Proc_MusicStyleMap_GetByMusicIds",
            new { MusicIds = tvp.AsTableValuedParameter("dbo.Tvp_NVarChar") },
            commandType: CommandType.StoredProcedure
        );

        return rows
            .GroupBy(x => x.MusicId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(x => x.StyleId).Distinct().ToArray(), StringComparer.OrdinalIgnoreCase);
    }

    public async Task<bool> DeleteAsync(string id)
    {
        using var conn = _db.Create();
        var affected = await conn.ExecuteScalarAsync<int>(
            "dbo.Proc_Musics_Delete",
            new { Id = id },
            commandType: CommandType.StoredProcedure
        );
        return affected > 0;
    }

    public async Task<(int TopicsUpserted, int StylesUpserted, int MusicsUpserted, int TopicMapRows, int StyleMapRows)> ImportAsync(
        IEnumerable<MusicTopic> topics,
        IEnumerable<MusicStyle> styles,
        IEnumerable<(Music Music, IReadOnlyCollection<int> TopicIds, IReadOnlyCollection<int> StyleIds)> musics)
    {
        var topicList = (topics ?? Array.Empty<MusicTopic>()).Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Name)).ToList();
        var styleList = (styles ?? Array.Empty<MusicStyle>()).Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Name)).ToList();
        var musicList = (musics ?? Array.Empty<(Music Music, IReadOnlyCollection<int> TopicIds, IReadOnlyCollection<int> StyleIds)>())
            .Where(x => x.Music != null && !string.IsNullOrWhiteSpace(x.Music.Id) && !string.IsNullOrWhiteSpace(x.Music.Name) && !string.IsNullOrWhiteSpace(x.Music.AudioUrl))
            .ToList();

        using var conn = _db.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();

        try
        {
            var topicsUpserted = 0;
            foreach (var t in topicList)
            {
                await conn.ExecuteAsync(
                    "dbo.Proc_MusicTopics_Upsert",
                    new { t.Id, t.Name, t.IsActive },
                    tx,
                    commandType: CommandType.StoredProcedure
                );
                topicsUpserted++;
            }

            var stylesUpserted = 0;
            foreach (var s in styleList)
            {
                await conn.ExecuteAsync(
                    "dbo.Proc_MusicStyles_Upsert",
                    new { s.Id, s.Name, s.IsActive },
                    tx,
                    commandType: CommandType.StoredProcedure
                );
                stylesUpserted++;
            }

            var validTopicIds = new HashSet<int>(
                await conn.QueryAsync<int>("SELECT Id FROM dbo.MusicTopics", transaction: tx),
                EqualityComparer<int>.Default
            );
            var validStyleIds = new HashSet<int>(
                await conn.QueryAsync<int>("SELECT Id FROM dbo.MusicStyles", transaction: tx),
                EqualityComparer<int>.Default
            );

            var musicsUpserted = 0;
            var topicMapRows = 0;
            var styleMapRows = 0;

            foreach (var item in musicList)
            {
                var m = item.Music;

                var filteredTopicIds = (item.TopicIds ?? Array.Empty<int>()).Where(id => id > 0 && validTopicIds.Contains(id)).Distinct().ToArray();
                var filteredStyleIds = (item.StyleIds ?? Array.Empty<int>()).Where(id => id > 0 && validStyleIds.Contains(id)).Distinct().ToArray();

                await conn.ExecuteAsync(
                    "dbo.Proc_Musics_Upsert",
                    new
                    {
                        m.Id,
                        m.IdStr,
                        m.Name,
                        m.Author,
                        m.Album,
                        m.Language,
                        m.Category,
                        DurationSeconds = m.DurationSeconds,
                        m.AudioUrl,
                        m.IsActive,
                    },
                    tx,
                    commandType: CommandType.StoredProcedure
                );
                musicsUpserted++;

                var topicTvp = new DataTable();
                topicTvp.Columns.Add("Value", typeof(int));
                foreach (var id in filteredTopicIds) { topicTvp.Rows.Add(id); topicMapRows++; }

                await conn.ExecuteAsync(
                    "dbo.Proc_MusicTopicMap_Replace",
                    new { MusicId = m.Id, TopicIds = topicTvp.AsTableValuedParameter("dbo.Tvp_Int") },
                    tx,
                    commandType: CommandType.StoredProcedure
                );

                var styleTvp = new DataTable();
                styleTvp.Columns.Add("Value", typeof(int));
                foreach (var id in filteredStyleIds) { styleTvp.Rows.Add(id); styleMapRows++; }

                await conn.ExecuteAsync(
                    "dbo.Proc_MusicStyleMap_Replace",
                    new { MusicId = m.Id, StyleIds = styleTvp.AsTableValuedParameter("dbo.Tvp_Int") },
                    tx,
                    commandType: CommandType.StoredProcedure
                );
            }

            tx.Commit();
            return (topicsUpserted, stylesUpserted, musicsUpserted, topicMapRows, styleMapRows);
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
