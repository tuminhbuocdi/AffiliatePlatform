using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using Management.Domain.Entities;
using Management.Infrastructure.External;
using Management.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Management.Api.Controllers;

[ApiController]
[Route("api/youtube")]
public class YoutubeController : ControllerBase
{
    private readonly YoutubeOAuthService _oauth;
    private readonly YoutubeApiClient _yt;
    private readonly YoutubeChannelRepository _repo;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public YoutubeController(YoutubeOAuthService oauth, YoutubeApiClient yt, YoutubeChannelRepository repo, IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _oauth = oauth;
        _yt = yt;
        _repo = repo;
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("oauth/url")]
    [Authorize]
    public ActionResult GetOAuthUrl()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var url = _oauth.GetAuthorizationUrl(userId.Value);
        return Ok(new { url });
    }

    [HttpGet("channels/{channelId}/oauth/url")]
    [Authorize]
    public ActionResult GetOAuthUrlForChannel([FromRoute] string channelId)
    {
        // For now, re-auth is the same OAuth flow; channelId is ignored and will be updated on callback.
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var url = _oauth.GetAuthorizationUrl(userId.Value);
        return Ok(new { url });
    }

    [HttpGet("oauth/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> OAuthCallback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error)
    {
        var frontendBase = _config["Frontend:BaseUrl"] ?? "http://localhost:5173";
        var redirect = frontendBase.TrimEnd('/') + "/admin/youtube";

        if (!string.IsNullOrWhiteSpace(error))
        {
            return Redirect(redirect + "?ytError=" + Uri.EscapeDataString(error));
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            return Redirect(redirect + "?ytError=" + Uri.EscapeDataString("Missing code/state"));
        }

        try
        {
            var userId = _oauth.ValidateStateAndGetUserId(state);
            var token = await _oauth.ExchangeCodeForToken(code);

            var accessToken = token.Access_Token;
            var refreshToken = token.Refresh_Token;

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return Redirect(redirect + "?ytError=" + Uri.EscapeDataString("Missing access token"));
            }

            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return Redirect(redirect + "?ytError=" + Uri.EscapeDataString("Missing refresh token (check prompt=consent & access_type=offline)"));
            }

            var now = DateTime.UtcNow;

            var channels = await _yt.GetMyChannels(accessToken);
            if (channels.Count == 0)
            {
                return Redirect(redirect + "?ytError=" + Uri.EscapeDataString("No channels found (mine/managedByMe)"));
            }

            foreach (var ch in channels)
            {
                if (string.IsNullOrWhiteSpace(ch.ChannelId)) continue;

                var existing = await _repo.GetByUserAndChannelId(userId, ch.ChannelId);

                var item = existing ?? new YoutubeChannelConnection
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ChannelId = ch.ChannelId,
                    CreatedAtUtc = now,
                };

                item.Title = ch.Title;
                item.ThumbnailUrl = ch.ThumbnailUrl;
                item.AccessToken = accessToken;
                item.RefreshToken = refreshToken;
                item.AccessTokenExpiresAtUtc = now.AddSeconds(token.Expires_In);
                item.Scope = token.Scope;
                item.IsRevoked = false;
                item.UpdatedAtUtc = now;

                await _repo.Upsert(item);
            }

            return Redirect(redirect + "?ytConnected=1");
        }
        catch (Exception ex)
        {
            return Redirect(redirect + "?ytError=" + Uri.EscapeDataString(ex.Message));
        }
    }

    [HttpGet("channels")]
    [Authorize]
    public async Task<IActionResult> GetChannels()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var rows = await _repo.GetByUserId(userId.Value);
        var result = rows.Select(x => new
        {
            id = x.ChannelId,
            title = x.Title,
            thumbnailUrl = x.ThumbnailUrl,
            isAuthorized = !x.IsRevoked,
            updatedAt = x.UpdatedAtUtc,
        });

        return Ok(result);
    }

    [HttpGet("channels/{channelId}")]
    [Authorize]
    public async Task<IActionResult> GetChannel([FromRoute] string channelId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var item = await _repo.GetByUserAndChannelId(userId.Value, channelId);
        if (item == null) return NotFound();

        return Ok(new
        {
            id = item.ChannelId,
            title = item.Title,
            thumbnailUrl = item.ThumbnailUrl,
            isAuthorized = !item.IsRevoked,
            updatedAt = item.UpdatedAtUtc,
            defaultDescription = item.DefaultDescription,
            defaultTags = item.DefaultTags,
        });
    }

    [HttpPut("channels/{channelId}/defaults")]
    [Authorize]
    public async Task<IActionResult> UpdateChannelDefaults([FromRoute] string channelId, [FromBody] UpdateChannelDefaultsRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var item = await _repo.GetByUserAndChannelId(userId.Value, channelId);
        if (item == null) return NotFound();

        item.DefaultDescription = request.DefaultDescription;
        item.DefaultTags = request.DefaultTags;
        item.UpdatedAtUtc = DateTime.UtcNow;

        await _repo.Upsert(item);
        return Ok(new { success = true });
    }

    public class UpdateChannelDefaultsRequest
    {
        public string? DefaultDescription { get; set; }
        public string? DefaultTags { get; set; }
    }

    public class CreatePlaylistRequest
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string PrivacyStatus { get; set; } = "public";
    }

    [HttpPost("channels/{channelId}/playlists")]
    [Authorize]
    public async Task<IActionResult> CreatePlaylist([FromRoute] string channelId, [FromBody] CreatePlaylistRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Title)) return BadRequest("Title is required.");
        if (string.IsNullOrWhiteSpace(req.PrivacyStatus)) return BadRequest("PrivacyStatus is required.");

        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var conn = await _yt.GetAuthorizedConnection(userId.Value, channelId);
        var playlistId = await _yt.CreatePlaylist(conn.AccessToken, channelId, req.Title, req.Description, req.PrivacyStatus);
        return Ok(new { success = true, playlistId });
    }

    public class UpdatePlaylistRequest
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string PrivacyStatus { get; set; } = "public";
    }

    [HttpPut("playlists/{playlistId}")]
    [Authorize]
    public async Task<IActionResult> UpdatePlaylist([FromRoute] string playlistId, [FromBody] UpdatePlaylistRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Title)) return BadRequest("Title is required.");
        if (string.IsNullOrWhiteSpace(req.PrivacyStatus)) return BadRequest("PrivacyStatus is required.");

        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var conn = await FindConnectionByVideoOwner(userId.Value);
        await _yt.UpdatePlaylist(conn.AccessToken, playlistId, req.Title, req.Description, req.PrivacyStatus);
        return Ok(new { success = true });
    }

    [HttpDelete("playlists/{playlistId}")]
    [Authorize]
    public async Task<IActionResult> DeletePlaylist([FromRoute] string playlistId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var conn = await FindConnectionByVideoOwner(userId.Value);
        await _yt.DeletePlaylist(conn.AccessToken, playlistId);
        return Ok(new { success = true });
    }

    [HttpGet("channels/{channelId}/videos/{videoId}/edit-info")]
    [Authorize]
    public async Task<IActionResult> GetVideoEditInfo([FromRoute] string channelId, [FromRoute] string videoId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var conn = await _yt.GetAuthorizedConnection(userId.Value, channelId);
        var info = await _yt.GetVideoEditInfo(conn.AccessToken, channelId, videoId);
        return Ok(new
        {
            tags = info.Tags,
            playlistIds = info.PlaylistIds,
        });
    }

    [HttpDelete("channels/{channelId}")]
    [Authorize]
    public async Task<IActionResult> DeleteChannel([FromRoute] string channelId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        await _repo.Revoke(userId.Value, channelId, DateTime.UtcNow);
        return Ok(new { success = true });
    }

    [HttpGet("channels/{channelId}/videos")]
    [Authorize]
    public async Task<IActionResult> GetVideos([FromRoute] string channelId, [FromQuery] string? query)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var conn = await _yt.GetAuthorizedConnection(userId.Value, channelId);
        var list = await _yt.ListChannelVideos(conn.AccessToken, channelId, query);

        var result = list.Select(v => new
        {
            id = v.Id,
            title = v.Title,
            description = v.Description,
            thumbnailUrl = v.ThumbnailUrl,
            publishedAt = v.PublishedAt,
            privacyStatus = v.PrivacyStatus,
            viewCount = v.ViewCount,
            commentCount = v.CommentCount,
            likeCount = v.LikeCount,
            dislikeCount = v.DislikeCount,
            tags = v.Tags,
        });

        return Ok(result);
    }

    public class UpdateVideoRequest
    {
        public string? ChannelId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string PrivacyStatus { get; set; } = "public";
        public string RegionCode { get; set; } = "VN";
        public string[]? Tags { get; set; }
        public DateTime? PublishAt { get; set; }
    }

    [HttpPut("videos/{videoId}")]
    [Authorize]
    public async Task<IActionResult> UpdateVideo([FromRoute] string videoId, [FromBody] UpdateVideoRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Title)) return BadRequest("Title is required.");
        if (string.IsNullOrWhiteSpace(req.PrivacyStatus)) return BadRequest("PrivacyStatus is required.");

        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        YoutubeChannelConnection conn;
        if (!string.IsNullOrWhiteSpace(req.ChannelId))
        {
            conn = await _yt.GetAuthorizedConnection(userId.Value, req.ChannelId);
        }
        else
        {
            conn = await FindConnectionByVideoOwner(userId.Value);
        }
        var desc = req.Description;
        if (desc != null && string.IsNullOrWhiteSpace(desc)) desc = null;
        var privacy = req.PrivacyStatus;
        if (req.PublishAt.HasValue && !string.Equals(privacy, "private", StringComparison.OrdinalIgnoreCase))
        {
            privacy = "private";
        }

        await _yt.UpdateVideo(conn.AccessToken, videoId, req.Title, desc, privacy, req.RegionCode, req.Tags, req.PublishAt);
        return Ok(new { success = true });
    }

    [HttpPost("videos/{videoId}/thumbnail")]
    [Authorize]
    [RequestSizeLimit(20971520)]
    public async Task<IActionResult> SetThumbnail([FromRoute] string videoId, IFormFile file)
    {
        if (file == null || file.Length <= 0) return BadRequest("File is required.");

        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var conn = await FindConnectionByVideoOwner(userId.Value);

        await using var stream = file.OpenReadStream();
        await _yt.SetVideoThumbnail(conn.AccessToken, videoId, stream, file.Length, file.ContentType);
        return Ok(new { success = true });
    }

    [HttpGet("channels/{channelId}/playlists")]
    [Authorize]
    public async Task<IActionResult> GetPlaylists([FromRoute] string channelId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var conn = await _yt.GetAuthorizedConnection(userId.Value, channelId);
        var list = await _yt.ListChannelPlaylists(conn.AccessToken, channelId);
        return Ok(list.Select(x => new { id = x.Id, title = x.Title }));
    }

    [HttpPost("playlists/{playlistId}/videos/{videoId}")]
    [Authorize]
    public async Task<IActionResult> AddVideoToPlaylist([FromRoute] string playlistId, [FromRoute] string videoId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var conn = await FindConnectionByVideoOwner(userId.Value);
        await _yt.AddVideoToPlaylist(conn.AccessToken, playlistId, videoId);
        return Ok(new { success = true });
    }

    public class BulkAddVideosToPlaylistRequest
    {
        public string[] VideoIds { get; set; } = Array.Empty<string>();
    }

    [HttpPost("playlists/{playlistId}/videos:bulk")]
    [Authorize]
    public async Task<IActionResult> BulkAddVideosToPlaylist([FromRoute] string playlistId, [FromBody] BulkAddVideosToPlaylistRequest req)
    {
        if (req.VideoIds == null || req.VideoIds.Length == 0) return BadRequest("VideoIds is required.");

        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var conn = await FindConnectionByVideoOwner(userId.Value);
        await _yt.AddVideosToPlaylist(conn.AccessToken, playlistId, req.VideoIds);
        return Ok(new { success = true });
    }

    [HttpDelete("playlists/{playlistId}/videos/{videoId}")]
    [Authorize]
    public async Task<IActionResult> RemoveVideoFromPlaylist([FromRoute] string playlistId, [FromRoute] string videoId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var conn = await FindConnectionByVideoOwner(userId.Value);
        await _yt.RemoveVideoFromPlaylist(conn.AccessToken, playlistId, videoId);
        return Ok(new { success = true });
    }

    [HttpDelete("videos/{videoId}")]
    [Authorize]
    public async Task<IActionResult> DeleteVideo([FromRoute] string videoId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var conn = await FindConnectionByVideoOwner(userId.Value);
        await _yt.DeleteVideo(conn.AccessToken, videoId);
        return Ok(new { success = true });
    }

    public class BatchUpdateVideosRequest
    {
        public string[] VideoIds { get; set; } = Array.Empty<string>();
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? PrivacyStatus { get; set; }
        public string RegionCode { get; set; } = "VN";
        public string[]? Tags { get; set; }
    }

    [HttpPut("videos:batch")]
    [Authorize]
    public async Task<IActionResult> BatchUpdateVideos([FromBody] BatchUpdateVideosRequest req)
    {
        if (req.VideoIds == null || req.VideoIds.Length == 0) return BadRequest("VideoIds is required.");
        var hasAnyUpdate = !string.IsNullOrWhiteSpace(req.Title)
            || req.Description != null
            || req.Tags != null
            || !string.IsNullOrWhiteSpace(req.PrivacyStatus);
        if (!hasAnyUpdate) return BadRequest("At least one of Title/Description/Tags/PrivacyStatus is required.");

        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var conn = await FindConnectionByVideoOwner(userId.Value);
        await _yt.BatchUpdateVideos(conn.AccessToken, req.VideoIds, req.Title, req.Description, req.PrivacyStatus, req.RegionCode, req.Tags);
        return Ok(new { success = true });
    }

    [HttpPost("channels/{channelId}/videos/upload")]
    [Authorize]
    [RequestSizeLimit(2147483647)]
    public async Task<IActionResult> UploadVideo(
        [FromRoute] string channelId,
        IFormFile file,
        [FromForm] string title,
        [FromForm] string? description,
        [FromForm] string privacyStatus,
        [FromForm] string[]? tags,
        [FromForm] string[]? playlistIds,
        [FromForm] string? publishAt)
    {
        if (file == null || file.Length <= 0) return BadRequest("File is required.");
        if (string.IsNullOrWhiteSpace(title)) return BadRequest("Title is required.");
        if (string.IsNullOrWhiteSpace(privacyStatus)) privacyStatus = "public";

        DateTime? publishAtUtc = null;
        if (!string.IsNullOrWhiteSpace(publishAt))
        {
            if (!DateTimeOffset.TryParse(publishAt, out var dto))
            {
                return BadRequest("publishAt is invalid (ISO datetime expected). ");
            }

            publishAtUtc = dto.UtcDateTime;

            if (publishAtUtc.Value <= DateTime.UtcNow.AddMinutes(1))
            {
                return BadRequest("publishAt must be in the future.");
            }

            privacyStatus = "private";
        }

        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var conn = await _yt.GetAuthorizedConnection(userId.Value, channelId);

        await using var stream = file.OpenReadStream();
        var videoId = await _yt.UploadVideoResumable(conn.AccessToken, stream, file.Length, title, description, privacyStatus, tags, publishAtUtc);

        if (!string.IsNullOrWhiteSpace(videoId) && playlistIds != null && playlistIds.Length > 0)
        {
            foreach (var pid in playlistIds.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                await _yt.AddVideoToPlaylist(conn.AccessToken, pid, videoId);
            }
        }

        return Ok(new { success = true, videoId });
    }

    public class UploadVideoFromUrlRequest
    {
        public string VideoUrl { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string PrivacyStatus { get; set; } = "public";
        public string[]? Tags { get; set; }
        public string[]? PlaylistIds { get; set; }
        public string? PublishAt { get; set; }
        public string? ThumbnailUrl { get; set; }
    }

    [HttpPost("channels/{channelId}/videos/upload-from-url")]
    [Authorize]
    [RequestSizeLimit(2147483647)]
    public async Task<IActionResult> UploadVideoFromUrl([FromRoute] string channelId, [FromBody] UploadVideoFromUrlRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.VideoUrl)) return BadRequest("VideoUrl is required.");
        if (string.IsNullOrWhiteSpace(req.Title)) return BadRequest("Title is required.");
        if (string.IsNullOrWhiteSpace(req.PrivacyStatus)) req.PrivacyStatus = "public";

        DateTime? publishAtUtc = null;
        if (!string.IsNullOrWhiteSpace(req.PublishAt))
        {
            if (!DateTimeOffset.TryParse(req.PublishAt, out var dto))
            {
                return BadRequest("publishAt is invalid (ISO datetime expected). ");
            }

            publishAtUtc = dto.UtcDateTime;
            if (publishAtUtc.Value <= DateTime.UtcNow.AddMinutes(1))
            {
                return BadRequest("publishAt must be in the future.");
            }

            req.PrivacyStatus = "private";
        }

        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var conn = await _yt.GetAuthorizedConnection(userId.Value, channelId);

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(10);

        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".mp4");
        try
        {
            using (var res = await client.GetAsync(req.VideoUrl.Trim(), HttpCompletionOption.ResponseHeadersRead))
            {
                if (!res.IsSuccessStatusCode)
                {
                    var err = await res.Content.ReadAsStringAsync();
                    return BadRequest($"Download video failed: {(int)res.StatusCode} {err}");
                }

                await using (var fs = System.IO.File.Create(tempPath))
                {
                    await using var rs = await res.Content.ReadAsStreamAsync();
                    await rs.CopyToAsync(fs);
                }
            }

            var fileInfo = new FileInfo(tempPath);
            if (!fileInfo.Exists || fileInfo.Length <= 0) return BadRequest("Downloaded video is empty");

            await using var stream = System.IO.File.OpenRead(tempPath);
            var videoId = await _yt.UploadVideoResumable(conn.AccessToken, stream, fileInfo.Length, req.Title, req.Description, req.PrivacyStatus, req.Tags, publishAtUtc);

            if (!string.IsNullOrWhiteSpace(videoId) && req.PlaylistIds != null && req.PlaylistIds.Length > 0)
            {
                foreach (var pid in req.PlaylistIds.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    await _yt.AddVideoToPlaylist(conn.AccessToken, pid, videoId);
                }
            }

            if (!string.IsNullOrWhiteSpace(videoId) && !string.IsNullOrWhiteSpace(req.ThumbnailUrl))
            {
                using var thumbRes = await client.GetAsync(req.ThumbnailUrl.Trim(), HttpCompletionOption.ResponseHeadersRead);
                if (thumbRes.IsSuccessStatusCode)
                {
                    var contentType = thumbRes.Content.Headers.ContentType?.MediaType ?? "image/*";
                    var len = thumbRes.Content.Headers.ContentLength;
                    await using var thumbStream = await thumbRes.Content.ReadAsStreamAsync();

                    // If content-length is unknown, buffer to memory to obtain size.
                    if (len == null || len <= 0)
                    {
                        await using var ms = new MemoryStream();
                        await thumbStream.CopyToAsync(ms);
                        ms.Position = 0;
                        await _yt.SetVideoThumbnail(conn.AccessToken, videoId, ms, ms.Length, contentType);
                    }
                    else
                    {
                        await _yt.SetVideoThumbnail(conn.AccessToken, videoId, thumbStream, len.Value, contentType);
                    }
                }
            }

            return Ok(new { success = true, videoId });
        }
        finally
        {
            try
            {
                if (System.IO.File.Exists(tempPath)) System.IO.File.Delete(tempPath);
            }
            catch
            {
                // ignore
            }
        }
    }

    [HttpGet("channels/{channelId}/analytics/summary")]
    [Authorize]
    public async Task<IActionResult> GetAnalyticsSummary([FromRoute] string channelId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var conn = await _yt.GetAuthorizedConnection(userId.Value, channelId);

        var end = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var start = end.AddDays(-28);

        var data = await _yt.GetAnalyticsSummary(conn.AccessToken, channelId, start, end);

        return Ok(new
        {
            watchTimeMinutes = data.WatchTimeMinutes,
            views = data.Views,
            avgViewDurationSec = data.AvgViewDurationSec,
            subsGained = data.SubsGained,
        });
    }

    private async Task<YoutubeChannelConnection> FindConnectionByVideoOwner(Guid userId)
    {
        var channels = await _repo.GetByUserId(userId);
        var first = channels.FirstOrDefault();
        if (first == null) throw new InvalidOperationException("No connected channel");

        var conn = await _yt.GetAuthorizedConnection(userId, first.ChannelId);
        return conn;
    }

    private Guid? GetUserId()
    {
        var idStr = User.FindFirstValue("uid") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(idStr, out var userId)) return userId;
        return null;
    }
}
