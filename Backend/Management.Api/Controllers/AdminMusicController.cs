using Management.Domain.Entities;
using Management.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Management.Api.Controllers;

[ApiController]
[Route("api/admin/music")]
[Authorize(Roles = "admin")]
public class AdminMusicController : ControllerBase
{
    private readonly MusicRepository _music;

    public AdminMusicController(MusicRepository music)
    {
        _music = music;
    }

    [HttpGet("topics")]
    public async Task<IActionResult> GetTopics()
    {
        var items = await _music.GetTopicsAsync();
        return Ok(items);
    }

    [HttpGet("styles")]
    public async Task<IActionResult> GetStyles()
    {
        var items = await _music.GetStylesAsync();
        return Ok(items);
    }

    public sealed class MusicItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string? IdStr { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Author { get; set; }
        public string? Album { get; set; }
        public string? Language { get; set; }
        public string? Category { get; set; }
        public double? DurationSeconds { get; set; }
        public string AudioUrl { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
        public int[] TopicIds { get; set; } = Array.Empty<int>();
        public int[] StyleIds { get; set; } = Array.Empty<int>();
    }

    public sealed class GetPageResponse
    {
        public int Total { get; set; }
        public MusicItemDto[] Items { get; set; } = Array.Empty<MusicItemDto>();
    }

    [HttpGet]
    public async Task<IActionResult> GetPage([FromQuery] string? q, [FromQuery] int? topicId, [FromQuery] int? styleId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var (items, total) = await _music.GetPageAsync(q, topicId, styleId, page, pageSize);

        var ids = items.Select(x => x.Id).ToArray();
        var topicMap = await _music.GetTopicMapAsync(ids);
        var styleMap = await _music.GetStyleMapAsync(ids);

        var dto = items.Select(x => new MusicItemDto
        {
            Id = x.Id,
            IdStr = x.IdStr,
            Name = x.Name,
            Author = x.Author,
            Album = x.Album,
            Language = x.Language,
            Category = x.Category,
            DurationSeconds = x.DurationSeconds,
            AudioUrl = x.AudioUrl,
            IsActive = x.IsActive,
            CreatedAtUtc = x.CreatedAtUtc,
            UpdatedAtUtc = x.UpdatedAtUtc,
            TopicIds = topicMap.TryGetValue(x.Id, out var t) ? t : Array.Empty<int>(),
            StyleIds = styleMap.TryGetValue(x.Id, out var s) ? s : Array.Empty<int>(),
        }).ToArray();

        return Ok(new GetPageResponse { Total = total, Items = dto });
    }

    public sealed class UpsertRequest
    {
        public string Id { get; set; } = string.Empty;
        public string? IdStr { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Author { get; set; }
        public string? Album { get; set; }
        public string? Language { get; set; }
        public string? Category { get; set; }
        public double? DurationSeconds { get; set; }
        public string AudioUrl { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public int[] TopicIds { get; set; } = Array.Empty<int>();
        public int[] StyleIds { get; set; } = Array.Empty<int>();
    }

    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] UpsertRequest req)
    {
        if (req == null) return BadRequest("Invalid request");
        req.Id = (req.Id ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(req.Id)) return BadRequest("Id is required");
        req.Name = (req.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("Name is required");
        req.AudioUrl = (req.AudioUrl ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(req.AudioUrl)) return BadRequest("AudioUrl is required");

        var music = new Music
        {
            Id = req.Id,
            IdStr = string.IsNullOrWhiteSpace(req.IdStr) ? null : req.IdStr.Trim(),
            Name = req.Name,
            Author = string.IsNullOrWhiteSpace(req.Author) ? null : req.Author.Trim(),
            Album = string.IsNullOrWhiteSpace(req.Album) ? null : req.Album.Trim(),
            Language = string.IsNullOrWhiteSpace(req.Language) ? null : req.Language.Trim(),
            Category = string.IsNullOrWhiteSpace(req.Category) ? null : req.Category.Trim(),
            DurationSeconds = req.DurationSeconds,
            AudioUrl = req.AudioUrl,
            IsActive = req.IsActive,
        };

        var saved = await _music.UpsertAsync(music);

        await _music.ReplaceTopicsAsync(saved.Id, (req.TopicIds ?? Array.Empty<int>()).Where(x => x > 0).Distinct().ToArray());
        await _music.ReplaceStylesAsync(saved.Id, (req.StyleIds ?? Array.Empty<int>()).Where(x => x > 0).Distinct().ToArray());

        var t = await _music.GetTopicMapAsync(new[] { saved.Id });
        var s = await _music.GetStyleMapAsync(new[] { saved.Id });

        return Ok(new MusicItemDto
        {
            Id = saved.Id,
            IdStr = saved.IdStr,
            Name = saved.Name,
            Author = saved.Author,
            Album = saved.Album,
            Language = saved.Language,
            Category = saved.Category,
            DurationSeconds = saved.DurationSeconds,
            AudioUrl = saved.AudioUrl,
            IsActive = saved.IsActive,
            CreatedAtUtc = saved.CreatedAtUtc,
            UpdatedAtUtc = saved.UpdatedAtUtc,
            TopicIds = t.TryGetValue(saved.Id, out var tt) ? tt : Array.Empty<int>(),
            StyleIds = s.TryGetValue(saved.Id, out var ss) ? ss : Array.Empty<int>(),
        });
    }

    public sealed class ImportRequest
    {
        public JsonElement Topic { get; set; }
        public JsonElement Style { get; set; }
        public JsonElement Music { get; set; }
    }

    public sealed class ImportResponse
    {
        public int TopicsUpserted { get; set; }
        public int StylesUpserted { get; set; }
        public int MusicsUpserted { get; set; }
        public int TopicMapRows { get; set; }
        public int StyleMapRows { get; set; }
    }

    [HttpPost("import")]
    public async Task<IActionResult> Import([FromBody] ImportRequest req)
    {
        if (req.Topic.ValueKind != JsonValueKind.Array) return BadRequest("topic must be array");
        if (req.Style.ValueKind != JsonValueKind.Array) return BadRequest("style must be array");
        if (req.Music.ValueKind != JsonValueKind.Array) return BadRequest("music must be array");

        static int ReadInt(JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n)) return n;
            if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var s)) return s;
            return 0;
        }

        static string? ReadString(JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.String) return el.GetString();
            if (el.ValueKind == JsonValueKind.Number) return el.GetRawText();
            return null;
        }

        var topics = new List<MusicTopic>();
        foreach (var el in req.Topic.EnumerateArray())
        {
            var id = el.TryGetProperty("id", out var idEl) ? ReadInt(idEl) : 0;
            var name = el.TryGetProperty("name", out var nameEl) ? (ReadString(nameEl) ?? string.Empty) : string.Empty;
            name = name.Trim();
            if (id > 0 && !string.IsNullOrWhiteSpace(name)) topics.Add(new MusicTopic { Id = id, Name = name, IsActive = true });
        }

        var styles = new List<MusicStyle>();
        foreach (var el in req.Style.EnumerateArray())
        {
            var id = el.TryGetProperty("id", out var idEl) ? ReadInt(idEl) : 0;
            var name = el.TryGetProperty("name", out var nameEl) ? (ReadString(nameEl) ?? string.Empty) : string.Empty;
            name = name.Trim();
            if (id > 0 && !string.IsNullOrWhiteSpace(name)) styles.Add(new MusicStyle { Id = id, Name = name, IsActive = true });
        }

        var musics = new List<(Music Music, IReadOnlyCollection<int> TopicIds, IReadOnlyCollection<int> StyleIds)>();
        foreach (var el in req.Music.EnumerateArray())
        {
            var id = el.TryGetProperty("id", out var idEl) ? (ReadString(idEl) ?? string.Empty) : string.Empty;
            var idStr = el.TryGetProperty("idStr", out var idStrEl) ? ReadString(idStrEl) : null;
            var name = el.TryGetProperty("name", out var nameEl) ? (ReadString(nameEl) ?? string.Empty) : string.Empty;
            var author = el.TryGetProperty("author", out var authorEl) ? ReadString(authorEl) : null;
            var album = el.TryGetProperty("album", out var albumEl) ? ReadString(albumEl) : null;
            var language = el.TryGetProperty("language", out var languageEl) ? ReadString(languageEl) : null;
            var category = el.TryGetProperty("category", out var categoryEl) ? ReadString(categoryEl) : null;
            var audioUrl = el.TryGetProperty("audioUrl", out var urlEl) ? ReadString(urlEl) : null;

            double? durationSeconds = null;
            if (el.TryGetProperty("duration", out var durEl))
            {
                if (durEl.ValueKind == JsonValueKind.Number && durEl.TryGetDouble(out var d)) durationSeconds = d;
                else if (durEl.ValueKind == JsonValueKind.String && double.TryParse(durEl.GetString(), out var ds)) durationSeconds = ds;
            }

            var topicIds = new List<int>();
            if (el.TryGetProperty("topicIds", out var topicIdsEl) && topicIdsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var x in topicIdsEl.EnumerateArray())
                {
                    var v = ReadInt(x);
                    if (v > 0) topicIds.Add(v);
                }
            }

            var styleIds = new List<int>();
            if (el.TryGetProperty("styleIds", out var styleIdsEl) && styleIdsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var x in styleIdsEl.EnumerateArray())
                {
                    var v = ReadInt(x);
                    if (v > 0) styleIds.Add(v);
                }
            }

            var m = new Music
            {
                Id = (id ?? string.Empty).Trim(),
                IdStr = string.IsNullOrWhiteSpace(idStr) ? null : idStr.Trim(),
                Name = (name ?? string.Empty).Trim(),
                Author = string.IsNullOrWhiteSpace(author) ? null : author.Trim(),
                Album = string.IsNullOrWhiteSpace(album) ? null : album.Trim(),
                Language = string.IsNullOrWhiteSpace(language) ? null : language.Trim(),
                Category = string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
                DurationSeconds = durationSeconds,
                AudioUrl = (audioUrl ?? string.Empty).Trim(),
                IsActive = true,
            };

            if (!string.IsNullOrWhiteSpace(m.Id) && !string.IsNullOrWhiteSpace(m.Name) && !string.IsNullOrWhiteSpace(m.AudioUrl))
            {
                musics.Add((m, topicIds, styleIds));
            }
        }

        var result = await _music.ImportAsync(topics, styles, musics);
        return Ok(new ImportResponse
        {
            TopicsUpserted = result.TopicsUpserted,
            StylesUpserted = result.StylesUpserted,
            MusicsUpserted = result.MusicsUpserted,
            TopicMapRows = result.TopicMapRows,
            StyleMapRows = result.StyleMapRows,
        });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete([FromRoute] string id)
    {
        id = (id ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(id)) return BadRequest("Invalid id");

        var ok = await _music.DeleteAsync(id);
        if (!ok) return NotFound();
        return Ok(new { ok = true });
    }
}
