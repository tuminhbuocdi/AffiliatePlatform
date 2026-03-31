using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using Management.Infrastructure.External;
using Management.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Management.Api.Controllers;

[ApiController]
[Route("api/facebook")]
public class FacebookController : ControllerBase
{
    private readonly FacebookOAuthService _oauth;
    private readonly FacebookApiClient _fb;
    private readonly FacebookPageRepository _repo;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public FacebookController(FacebookOAuthService oauth, FacebookApiClient fb, FacebookPageRepository repo, IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _oauth = oauth;
        _fb = fb;
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

    [HttpGet("oauth/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> OAuthCallback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error)
    {
        var frontendBase = _config["Frontend:BaseUrl"] ?? "http://localhost:5173";
        var redirect = frontendBase.TrimEnd('/') + "/admin/facebook";

        if (!string.IsNullOrWhiteSpace(error))
        {
            return Redirect(redirect + "?fbError=" + Uri.EscapeDataString(error));
        }

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            return Redirect(redirect + "?fbError=" + Uri.EscapeDataString("Missing code/state"));
        }

        try
        {
            var userId = _oauth.ValidateStateAndGetUserId(state);

            var token = await _oauth.ExchangeCodeForToken(code);
            var accessToken = token.Access_Token;
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return Redirect(redirect + "?fbError=" + Uri.EscapeDataString("Missing access token"));
            }

            // Prefer long-lived token for better UX
            var longLived = await _oauth.ExchangeForLongLivedToken(accessToken);
            var finalAccessToken = longLived.Access_Token ?? accessToken;
            var expiresIn = longLived.Expires_In > 0 ? longLived.Expires_In : token.Expires_In;

            // Load pages and store their page access token
            await _fb.UpsertPages(userId, finalAccessToken, expiresIn, string.Join(',', _oauth.GetScopes()));

            return Redirect(redirect + "?fbConnected=1");
        }
        catch (Exception ex)
        {
            return Redirect(redirect + "?fbError=" + Uri.EscapeDataString(ex.Message));
        }
    }

    [HttpGet("pages")]
    [Authorize]
    public async Task<IActionResult> GetConnectedPages()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var rows = await _repo.GetByUserId(userId.Value);
        var result = rows.Select(x => new
        {
            id = x.PageId,
            name = x.PageName,
            pictureUrl = x.PictureUrl,
            isRevoked = x.IsRevoked,
            updatedAtUtc = x.UpdatedAtUtc,
        });

        return Ok(result);
    }

    [HttpGet("pages/{pageId}")]
    [Authorize]
    public async Task<IActionResult> GetPage([FromRoute] string pageId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var item = await _repo.GetByUserAndPageId(userId.Value, pageId);
        if (item == null) return NotFound();

        return Ok(new
        {
            id = item.PageId,
            name = item.PageName,
            pictureUrl = item.PictureUrl,
            isRevoked = item.IsRevoked,
            updatedAtUtc = item.UpdatedAtUtc,
            defaultTitle = item.DefaultTitle,
            defaultDescription = item.DefaultDescription,
            defaultLink = item.DefaultLink,
            defaultMode = item.DefaultMode,
        });
    }

    public class UpdatePageDefaultsRequest
    {
        public string? DefaultTitle { get; set; }
        public string? DefaultDescription { get; set; }
        public string? DefaultLink { get; set; }
        public string? DefaultMode { get; set; }
    }

    [HttpPut("pages/{pageId}/defaults")]
    [Authorize]
    public async Task<IActionResult> UpdatePageDefaults([FromRoute] string pageId, [FromBody] UpdatePageDefaultsRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var item = await _repo.GetByUserAndPageId(userId.Value, pageId);
        if (item == null) return BadRequest("Facebook page is not connected. Please sync pages / reconnect Facebook first.");

        item.DefaultTitle = request.DefaultTitle;
        item.DefaultDescription = request.DefaultDescription;
        item.DefaultLink = request.DefaultLink;
        item.DefaultMode = request.DefaultMode;
        item.UpdatedAtUtc = DateTime.UtcNow;

        await _repo.Upsert(item);
        return Ok(new { success = true });
    }

    [HttpPost("pages/sync")]
    [Authorize]
    public async Task<IActionResult> SyncPages()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var any = await _repo.GetAnyActiveByUserId(userId.Value);
        if (any == null || string.IsNullOrWhiteSpace(any.UserAccessToken))
        {
            return BadRequest("Facebook is not connected");
        }

        var expiresIn = 0;
        if (any.AccessTokenExpiresAtUtc.HasValue)
        {
            var secs = (int)Math.Floor((any.AccessTokenExpiresAtUtc.Value - DateTime.UtcNow).TotalSeconds);
            expiresIn = secs > 0 ? secs : 0;
        }

        await _fb.UpsertPages(userId.Value, any.UserAccessToken, expiresIn, any.Scope);
        return Ok(new { success = true });
    }

    public class CreatePostRequest
    {
        public string PageId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Link { get; set; }
        public string? PublishAt { get; set; }
    }

    [HttpPost("reels")]
    [Authorize]
    [RequestSizeLimit(2147483647)]
    public async Task<IActionResult> UploadReel(
        IFormFile video,
        [FromForm] string pageId,
        [FromForm] string? title,
        [FromForm] string? description,
        [FromForm] string? link)
    {
        if (string.IsNullOrWhiteSpace(pageId)) return BadRequest("pageId is required");
        if (video == null || video.Length <= 0) return BadRequest("video is required");

        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var page = await _fb.GetAuthorizedPage(userId.Value, pageId);

        // Note: CTA button is not supported for organic reels via Graph API.
        // If link is provided, append it to description so it appears in caption.
        var finalDesc = description;
        if (!string.IsNullOrWhiteSpace(link))
        {
            finalDesc = string.IsNullOrWhiteSpace(finalDesc)
                ? link
                : (finalDesc.TrimEnd() + "\n" + link);
        }

        string videoId;
        try
        {
            videoId = await _fb.CreateReelStart(page.PageAccessToken);
        }
        catch (Exception ex)
        {
            return BadRequest($"reels:start failed: {ex.Message}");
        }

        try
        {
            await using var stream = video.OpenReadStream();
            await _fb.UploadReelBinary(page.PageAccessToken, videoId, stream, video.Length);
        }
        catch (Exception ex)
        {
            return BadRequest($"reels:upload failed (videoId={videoId}): {ex.Message}");
        }

        try
        {
            await _fb.PublishReelFinish(page.PageAccessToken, videoId, finalDesc, title);
        }
        catch (Exception ex)
        {
            return BadRequest($"reels:finish failed (videoId={videoId}): {ex.Message}");
        }

        return Ok(new { success = true, videoId });
    }

    [HttpPost("posts")]
    [Authorize]
    public async Task<IActionResult> CreatePost([FromBody] CreatePostRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.PageId)) return BadRequest("PageId is required");
        if (string.IsNullOrWhiteSpace(req.Message)) return BadRequest("Message is required");

        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var page = await _fb.GetAuthorizedPage(userId.Value, req.PageId);
        DateTimeOffset? publishAtUtc = null;
        if (!string.IsNullOrWhiteSpace(req.PublishAt))
        {
            if (!DateTimeOffset.TryParse(req.PublishAt, out var dto)) return BadRequest("PublishAt is invalid");
            publishAtUtc = dto.ToUniversalTime();
        }

        var postId = await _fb.CreatePost(page.PageAccessToken, page.PageId, req.Message, req.Link, publishAtUtc);
        return Ok(new { success = true, postId });
    }

    public class CreatePostWithMediaRequest
    {
        public string PageId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Link { get; set; }
        public List<string> ImageUrls { get; set; } = new();
        public List<string> VideoUrls { get; set; } = new();
        public string? PublishAt { get; set; }
    }

    [HttpPost("posts/with-media")]
    [Authorize]
    public async Task<IActionResult> CreatePostWithMedia([FromBody] CreatePostWithMediaRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.PageId)) return BadRequest("PageId is required");
        if (string.IsNullOrWhiteSpace(req.Message)) return BadRequest("Message is required");

        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var page = await _fb.GetAuthorizedPage(userId.Value, req.PageId);

        DateTimeOffset? publishAtUtc = null;
        if (!string.IsNullOrWhiteSpace(req.PublishAt))
        {
            if (!DateTimeOffset.TryParse(req.PublishAt, out var dto)) return BadRequest("PublishAt is invalid");
            publishAtUtc = dto.ToUniversalTime();
        }

        if (publishAtUtc.HasValue && req.VideoUrls != null && req.VideoUrls.Any(x => !string.IsNullOrWhiteSpace(x)))
        {
            return BadRequest("Scheduling posts with videos is not supported.");
        }

        var photoIds = new List<string>();
        var createdVideoPostIds = new List<string>();

        if (req.ImageUrls != null)
        {
            foreach (var url in req.ImageUrls)
            {
                if (string.IsNullOrWhiteSpace(url)) continue;
                var id = await _fb.UploadPhotoFromUrlUnpublished(page.PageAccessToken, page.PageId, url.Trim());
                if (!string.IsNullOrWhiteSpace(id)) photoIds.Add(id);
            }
        }

        if (req.VideoUrls != null)
        {
            foreach (var url in req.VideoUrls)
            {
                if (string.IsNullOrWhiteSpace(url)) continue;
                // Facebook does not support attaching video IDs via attached_media on /feed reliably.
                // Publish videos directly via /{pageId}/videos.
                var vid = await _fb.CreateVideoPostFromUrl(page.PageAccessToken, page.PageId, url.Trim(), req.Message);
                if (!string.IsNullOrWhiteSpace(vid)) createdVideoPostIds.Add(vid);
            }
        }

        if (photoIds.Count == 0 && createdVideoPostIds.Count == 0)
        {
            var postIdNoMedia = await _fb.CreatePost(page.PageAccessToken, page.PageId, req.Message, req.Link, publishAtUtc);
            return Ok(new { success = true, postId = postIdNoMedia, mediaCount = 0 });
        }

        string? postId = null;
        if (photoIds.Count > 0)
        {
            postId = await _fb.CreatePostWithAttachedMedia(page.PageAccessToken, page.PageId, req.Message, photoIds, req.Link, publishAtUtc);
        }

        return Ok(new
        {
            success = true,
            postId,
            photoCount = photoIds.Count,
            videoCount = createdVideoPostIds.Count,
            videoPostIds = createdVideoPostIds,
        });
    }

    public class UploadReelFromUrlRequest
    {
        public string PageId { get; set; } = string.Empty;
        public string VideoUrl { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Link { get; set; }
    }

    [HttpPost("reels/from-url")]
    [Authorize]
    [RequestSizeLimit(2147483647)]
    public async Task<IActionResult> UploadReelFromUrl([FromBody] UploadReelFromUrlRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.PageId)) return BadRequest("PageId is required");
        if (string.IsNullOrWhiteSpace(req.VideoUrl)) return BadRequest("VideoUrl is required");

        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var page = await _fb.GetAuthorizedPage(userId.Value, req.PageId);

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(5);

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

            var finalDesc = req.Description;
            if (!string.IsNullOrWhiteSpace(req.Link))
            {
                finalDesc = string.IsNullOrWhiteSpace(finalDesc) ? req.Link : (finalDesc.TrimEnd() + "\n" + req.Link);
            }

            var videoId = await _fb.CreateReelStart(page.PageAccessToken);

            await using (var fs = System.IO.File.OpenRead(tempPath))
            {
                await _fb.UploadReelBinary(page.PageAccessToken, videoId, fs, fileInfo.Length);
            }

            await _fb.PublishReelFinish(page.PageAccessToken, videoId, finalDesc, req.Title);
            return Ok(new { success = true, videoId });
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
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

    public class UpdatePostRequest
    {
        public string PageId { get; set; } = string.Empty;
        public string PostId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    [HttpPut("posts")]
    [Authorize]
    public async Task<IActionResult> UpdatePost([FromBody] UpdatePostRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.PageId)) return BadRequest("PageId is required");
        if (string.IsNullOrWhiteSpace(req.PostId)) return BadRequest("PostId is required");
        if (string.IsNullOrWhiteSpace(req.Message)) return BadRequest("Message is required");

        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var page = await _fb.GetAuthorizedPage(userId.Value, req.PageId);
        await _fb.UpdatePost(page.PageAccessToken, req.PostId, req.Message);
        return Ok(new { success = true });
    }

    [HttpDelete("posts/{postId}")]
    [Authorize]
    public async Task<IActionResult> DeletePost([FromRoute] string postId, [FromQuery] string pageId)
    {
        if (string.IsNullOrWhiteSpace(pageId)) return BadRequest("pageId is required");

        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var page = await _fb.GetAuthorizedPage(userId.Value, pageId);
        await _fb.DeletePost(page.PageAccessToken, postId);
        return Ok(new { success = true });
    }

    [HttpGet("pages/{pageId}/posts")]
    [Authorize]
    public async Task<IActionResult> ListPosts([FromRoute] string pageId, [FromQuery] int limit = 25)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var page = await _fb.GetAuthorizedPage(userId.Value, pageId);
        var items = await _fb.ListPosts(page.PageAccessToken, page.PageId, limit);

        var result = items.Select(x => new
        {
            id = x.Id,
            message = x.Message,
            createdTime = x.CreatedTime,
            permalinkUrl = x.PermalinkUrl,
            pictureUrl = x.PictureUrl,
            likeCount = x.LikeCount,
            commentCount = x.CommentCount,
            shareCount = x.ShareCount,
            viewCount = x.ViewCount,
        });

        return Ok(result);
    }

    private Guid? GetUserId()
    {
        var val = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(val, out var id)) return id;
        return null;
    }
}
