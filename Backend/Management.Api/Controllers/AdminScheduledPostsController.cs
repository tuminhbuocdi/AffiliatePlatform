using Management.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Management.Api.Controllers;

[ApiController]
[Route("api/admin/scheduled-posts")]
[Authorize(Roles = "admin")]
public sealed class AdminScheduledPostsController : ControllerBase
{
    private readonly ScheduledPostRepository _repo;

    public AdminScheduledPostsController(ScheduledPostRepository repo)
    {
        _repo = repo;
    }

    public sealed class AssetDto
    {
        public string Type { get; set; } = string.Empty; // image|video
        public string Url { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }

    public sealed class TargetsDto
    {
        public bool Facebook { get; set; }
        public bool Tiktok { get; set; }
        public bool Youtube { get; set; }

        public string[]? FbPageIds { get; set; }
        public string? FbTitle { get; set; }
        public string? FbDescription { get; set; }
        public string? FbLink { get; set; }

        public string[]? YtChannelIds { get; set; }
        public Dictionary<string, string[]>? YtPlaylistIdsByChannel { get; set; }
        public string? YtTitle { get; set; }
        public string? YtDescription { get; set; }
        public string? YtTagsText { get; set; }
        public string? YtPrivacyStatus { get; set; }
        public string? YtPublishAt { get; set; }
    }

    public sealed class CreateRequest
    {
        public string? PageId { get; set; }
        public string Caption { get; set; } = string.Empty;
        public string ScheduledAtLocal { get; set; } = string.Empty; // ISO, UTC+7
        public string? Timezone { get; set; } = "Asia/Ho_Chi_Minh";
        public TargetsDto Targets { get; set; } = new();
        public AssetDto[] Assets { get; set; } = Array.Empty<AssetDto>();
    }

    public sealed class ScheduledPostDto
    {
        public Guid Id { get; set; }
        public string? PageId { get; set; }
        public string Caption { get; set; } = string.Empty;
        public string ScheduledAtLocal { get; set; } = string.Empty;
        public string ScheduledAtUtc { get; set; } = string.Empty;
        public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";
        public string TargetsJson { get; set; } = "{}";
        public string Status { get; set; } = string.Empty;
        public string? RemindedAtUtc { get; set; }
        public AssetDto[] Assets { get; set; } = Array.Empty<AssetDto>();
    }

    private static ScheduledPostDto Map(ScheduledPostRepository.ScheduledPostRow p, IReadOnlyList<ScheduledPostRepository.ScheduledPostAssetRow> assets)
    {
        var a = assets
            .Where(x => x.ScheduledPostId == p.Id)
            .OrderBy(x => x.SortOrder)
            .Select(x => new AssetDto { Type = x.AssetType, Url = x.Url, SortOrder = x.SortOrder })
            .ToArray();

        return new ScheduledPostDto
        {
            Id = p.Id,
            PageId = p.PageId,
            Caption = p.Caption,
            ScheduledAtLocal = p.ScheduledAtLocal.ToString("yyyy-MM-ddTHH:mm:ss"),
            ScheduledAtUtc = p.ScheduledAtUtc.ToString("o"),
            Timezone = p.Timezone,
            TargetsJson = p.TargetsJson,
            Status = p.Status,
            RemindedAtUtc = p.RemindedAtUtc?.ToString("o"),
            Assets = a,
        };
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRequest req)
    {
        if (req == null) return BadRequest("Invalid request");
        if (string.IsNullOrWhiteSpace(req.Caption)) return BadRequest("Caption is required");
        if (string.IsNullOrWhiteSpace(req.ScheduledAtLocal)) return BadRequest("ScheduledAtLocal is required");

        if (!DateTimeOffset.TryParse(req.ScheduledAtLocal, out var dto))
        {
            // Expected: 2026-03-22T15:30:00+07:00 or local browser's ISO.
            return BadRequest("ScheduledAtLocal is invalid (ISO datetime expected)");
        }

        // Store local time components and UTC.
        var scheduledUtc = dto.ToUniversalTime().UtcDateTime;
        var scheduledLocal = dto.DateTime;

        var targetsJson = JsonSerializer.Serialize(req.Targets ?? new TargetsDto());

        var assets = (req.Assets ?? Array.Empty<AssetDto>())
            .Where(x => !string.IsNullOrWhiteSpace(x?.Url) && !string.IsNullOrWhiteSpace(x?.Type))
            .Select(x => ((x.Type ?? string.Empty).Trim().ToLowerInvariant(), (x.Url ?? string.Empty).Trim(), x.SortOrder))
            .ToList();

        var id = await _repo.CreateAsync(new ScheduledPostRepository.CreateScheduledPost(
            CreatedBy: null,
            PageId: string.IsNullOrWhiteSpace(req.PageId) ? null : req.PageId.Trim(),
            Caption: req.Caption,
            ScheduledAtLocal: scheduledLocal,
            ScheduledAtUtc: scheduledUtc,
            Timezone: string.IsNullOrWhiteSpace(req.Timezone) ? "Asia/Ho_Chi_Minh" : req.Timezone!,
            TargetsJson: targetsJson,
            Assets: assets
        ));

        var (posts, postAssets) = await _repo.GetByIdsAsync(new[] { id });
        var p = posts.FirstOrDefault();
        if (p == null) return StatusCode(500, "Failed to load created scheduled post");
        return Ok(Map(p, postAssets));
    }

    [HttpGet]
    public async Task<IActionResult> GetRange([FromQuery] string fromUtc, [FromQuery] string toUtc, [FromQuery] string? status)
    {
        if (!DateTimeOffset.TryParse(fromUtc, out var f)) return BadRequest("fromUtc is invalid");
        if (!DateTimeOffset.TryParse(toUtc, out var t)) return BadRequest("toUtc is invalid");

        var (posts, assets) = await _repo.GetRangeAsync(f.UtcDateTime, t.UtcDateTime, string.IsNullOrWhiteSpace(status) ? null : status.Trim());
        var dto = posts.Select(x => Map(x, assets)).ToArray();
        return Ok(dto);
    }

    [HttpPost("due")]
    public async Task<IActionResult> GetDueAndMark()
    {
        var now = DateTime.UtcNow;
        var (posts, assets) = await _repo.GetDueAndMarkAsync(now);
        var dto = posts.Select(x => Map(x, assets)).ToArray();
        return Ok(dto);
    }

    [HttpPost("{id:guid}/mark-done")]
    public async Task<IActionResult> MarkDone([FromRoute] Guid id)
    {
        var ok = await _repo.MarkDoneAsync(id, DateTime.UtcNow);
        return Ok(new { success = ok });
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel([FromRoute] Guid id)
    {
        var ok = await _repo.CancelAsync(id, DateTime.UtcNow);
        return Ok(new { success = ok });
    }
}
