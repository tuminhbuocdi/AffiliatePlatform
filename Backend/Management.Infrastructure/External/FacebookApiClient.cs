using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Management.Domain.Entities;
using Management.Infrastructure.Repositories;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System.Net;

namespace Management.Infrastructure.External;

public class FacebookApiClient
{
    private readonly HttpClient _http;
    private readonly FacebookOAuthService _oauth;
    private readonly FacebookPageRepository _repo;
    private readonly IConfiguration _config;

    public FacebookApiClient(HttpClient http, FacebookOAuthService oauth, FacebookPageRepository repo, IConfiguration config)
    {
        _http = http;
        _oauth = oauth;
        _repo = repo;
        _config = config;
    }

    private string GraphVersion => _config["Facebook:GraphApiVersion"] ?? "v20.0";

    private string Graph(string pathAndQuery)
    {
        if (pathAndQuery.StartsWith("/"))
        {
            return $"https://graph.facebook.com/{GraphVersion}{pathAndQuery}";
        }

        return $"https://graph.facebook.com/{GraphVersion}/{pathAndQuery}";
    }

    private string RuploadVideo(string videoId)
    {
        var id = Uri.EscapeDataString(videoId);
        return $"https://rupload.facebook.com/video-upload/{GraphVersion}/{id}";
    }

    public async Task<FacebookPageConnection> GetAuthorizedPage(Guid userId, string pageId)
    {
        var conn = await _repo.GetByUserAndPageId(userId, pageId);
        if (conn == null || conn.IsRevoked)
        {
            throw new InvalidOperationException("Facebook page is not connected");
        }

        return conn;
    }

    public async Task<IReadOnlyList<MyPageItem>> GetMyPages(string userAccessToken)
    {
        // /me/accounts returns pages with access_token
        var url = Graph("/me/accounts?fields=id,name,access_token,picture.type(square){url}&limit=200");
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userAccessToken);

        var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException(body);

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<MyPageItem>();
        }

        var list = new List<MyPageItem>();
        foreach (var it in data.EnumerateArray())
        {
            var id = it.TryGetProperty("id", out var vId) ? vId.GetString() : null;
            var name = it.TryGetProperty("name", out var vName) ? vName.GetString() : null;
            var token = it.TryGetProperty("access_token", out var vTok) ? vTok.GetString() : null;

            string? pictureUrl = null;
            if (it.TryGetProperty("picture", out var pic)
                && pic.TryGetProperty("data", out var picData)
                && picData.TryGetProperty("url", out var picUrl))
            {
                pictureUrl = picUrl.GetString();
            }

            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            list.Add(new MyPageItem
            {
                PageId = id,
                PageName = name,
                PageAccessToken = token,
                PictureUrl = pictureUrl,
            });
        }

        return list;
    }

    public async Task UpsertPages(Guid userId, string userAccessToken, int expiresInSeconds, string? scope)
    {
        var pages = await GetMyPages(userAccessToken);
        var now = DateTime.UtcNow;
        var expiresAt = expiresInSeconds > 0 ? now.AddSeconds(expiresInSeconds) : (DateTime?)null;

        foreach (var p in pages)
        {
            var existing = await _repo.GetByUserAndPageId(userId, p.PageId);

            var item = existing ?? new FacebookPageConnection
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PageId = p.PageId,
                CreatedAtUtc = now,
            };

            item.PageName = p.PageName;
            item.PictureUrl = p.PictureUrl;
            item.UserAccessToken = userAccessToken;
            item.PageAccessToken = p.PageAccessToken;
            item.AccessTokenExpiresAtUtc = expiresAt;
            item.Scope = scope;
            item.IsRevoked = false;
            item.UpdatedAtUtc = now;

            await _repo.Upsert(item);
        }
    }

    public async Task<string> CreatePost(string pageAccessToken, string pageId, string message, string? link = null, DateTimeOffset? publishAtUtc = null)
    {
        var url = Graph($"{Uri.EscapeDataString(pageId)}/feed");

        var form = new Dictionary<string, string>
        {
            ["message"] = message,
        };
        if (!string.IsNullOrWhiteSpace(link)) form["link"] = link;

        if (publishAtUtc.HasValue)
        {
            var unix = publishAtUtc.Value.ToUnixTimeSeconds();
            form["published"] = "false";
            form["scheduled_publish_time"] = unix.ToString();
            form["unpublished_content_type"] = "SCHEDULED";
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(form),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", pageAccessToken);

        var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException(body);

        using var doc = JsonDocument.Parse(body);
        var postId = doc.RootElement.TryGetProperty("id", out var vId) ? vId.GetString() : null;
        return postId ?? string.Empty;
    }

    public async Task<string> UploadPhotoFromUrlUnpublished(string pageAccessToken, string pageId, string photoUrl)
    {
        var url = Graph($"{Uri.EscapeDataString(pageId)}/photos");

        var form = new Dictionary<string, string>
        {
            ["url"] = photoUrl,
            ["published"] = "false",
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(form),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", pageAccessToken);

        var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException(body);

        using var doc = JsonDocument.Parse(body);
        var id = doc.RootElement.TryGetProperty("id", out var vId) ? vId.GetString() : null;
        return id ?? string.Empty;
    }

    public async Task<string> CreateVideoPostFromUrl(string pageAccessToken, string pageId, string videoUrl, string? description)
    {
        var url = Graph($"{Uri.EscapeDataString(pageId)}/videos");

        var form = new Dictionary<string, string>
        {
            ["file_url"] = videoUrl,
            ["published"] = "true",
        };
        if (!string.IsNullOrWhiteSpace(description)) form["description"] = description;

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(form),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", pageAccessToken);

        var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException(body);

        using var doc = JsonDocument.Parse(body);
        var id = doc.RootElement.TryGetProperty("id", out var vId) ? vId.GetString() : null;
        return id ?? string.Empty;
    }

    public async Task<string> UploadVideoFromUrlUnpublished(string pageAccessToken, string pageId, string videoUrl, string? description)
    {
        var url = Graph($"{Uri.EscapeDataString(pageId)}/videos");

        var form = new Dictionary<string, string>
        {
            ["file_url"] = videoUrl,
            ["published"] = "false",
        };
        if (!string.IsNullOrWhiteSpace(description)) form["description"] = description;

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(form),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", pageAccessToken);

        var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException(body);

        using var doc = JsonDocument.Parse(body);
        var id = doc.RootElement.TryGetProperty("id", out var vId) ? vId.GetString() : null;
        return id ?? string.Empty;
    }

    public async Task<string> CreatePostWithAttachedMedia(string pageAccessToken, string pageId, string message, IReadOnlyList<string> mediaFbIds, string? link = null, DateTimeOffset? publishAtUtc = null)
    {
        var url = Graph($"{Uri.EscapeDataString(pageId)}/feed");

        var form = new Dictionary<string, string>
        {
            ["message"] = message,
        };
        if (!string.IsNullOrWhiteSpace(link)) form["link"] = link;

        if (publishAtUtc.HasValue)
        {
            var unix = publishAtUtc.Value.ToUnixTimeSeconds();
            form["published"] = "false";
            form["scheduled_publish_time"] = unix.ToString();
            form["unpublished_content_type"] = "SCHEDULED";
        }

        for (var i = 0; i < mediaFbIds.Count; i++)
        {
            var mediaId = mediaFbIds[i];
            if (string.IsNullOrWhiteSpace(mediaId)) continue;
            form[$"attached_media[{i}]"] = $"{{\"media_fbid\":\"{mediaId}\"}}";
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(form),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", pageAccessToken);

        var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException(body);

        using var doc = JsonDocument.Parse(body);
        var id = doc.RootElement.TryGetProperty("id", out var vId) ? vId.GetString() : null;
        return id ?? string.Empty;
    }

    public async Task<string> CreateReelStart(string pageAccessToken)
    {
        var url = Graph("/me/video_reels");

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["upload_phase"] = "start",
            }),
        };

        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", pageAccessToken);

        var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException(body);

        using var doc = JsonDocument.Parse(body);
        var videoId = doc.RootElement.TryGetProperty("video_id", out var vId) ? vId.GetString() : null;
        if (string.IsNullOrWhiteSpace(videoId))
        {
            throw new InvalidOperationException("Missing video_id");
        }

        return videoId;
    }

    public async Task UploadReelBinary(string pageAccessToken, string videoId, Stream stream, long fileSize)
    {
        var url = RuploadVideo(videoId);

        using var content = new StreamContent(stream);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = content,
        };

        // Rupload requires OAuth scheme
        req.Headers.TryAddWithoutValidation("Authorization", $"OAuth {pageAccessToken}");
        req.Headers.TryAddWithoutValidation("offset", "0");
        req.Headers.TryAddWithoutValidation("file_size", fileSize.ToString());

        var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException(body);
    }

    public async Task PublishReelFinish(string pageAccessToken, string videoId, string? description, string? title)
    {
        var url = Graph("/me/video_reels");

        var form = new Dictionary<string, string>
        {
            ["upload_phase"] = "finish",
            ["video_id"] = videoId,
            ["video_state"] = "PUBLISHED",
        };

        if (!string.IsNullOrWhiteSpace(description)) form["description"] = description;
        if (!string.IsNullOrWhiteSpace(title)) form["title"] = title;

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(form),
        };

        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", pageAccessToken);

        var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException(body);
    }

    public async Task UpdatePost(string pageAccessToken, string postId, string message)
    {
        var url = Graph(Uri.EscapeDataString(postId));

        var form = new Dictionary<string, string>
        {
            ["message"] = message,
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(form),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", pageAccessToken);

        var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException(body);
    }

    public async Task DeletePost(string pageAccessToken, string postId)
    {
        var url = Graph(Uri.EscapeDataString(postId));
        using var req = new HttpRequestMessage(HttpMethod.Delete, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", pageAccessToken);

        var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException(body);
    }

    public async Task<IReadOnlyList<PostItem>> ListPosts(string pageAccessToken, string pageId, int limit = 25)
    {
        var url = Graph($"{Uri.EscapeDataString(pageId)}/posts?fields=id,message,created_time,permalink_url,full_picture,shares,reactions.summary(true).limit(0),comments.summary(true).limit(0),attachments{{media_type,media{{image{{src}}}}}}&limit={limit}");
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", pageAccessToken);

        var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException(body);

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<PostItem>();
        }

        var list = new List<PostItem>();
        foreach (var it in data.EnumerateArray())
        {
            var id = it.TryGetProperty("id", out var vId) ? vId.GetString() : null;
            if (string.IsNullOrWhiteSpace(id)) continue;

            var msg = it.TryGetProperty("message", out var vMsg) ? vMsg.GetString() : null;
            var permalink = it.TryGetProperty("permalink_url", out var vPerm) ? vPerm.GetString() : null;

            DateTime? created = null;
            if (it.TryGetProperty("created_time", out var vCreated) && vCreated.ValueKind == JsonValueKind.String)
            {
                if (DateTime.TryParse(vCreated.GetString(), out var dt)) created = dt;
            }

            var reactionCount = 0;
            if (it.TryGetProperty("reactions", out var vReactions)
                && vReactions.TryGetProperty("summary", out var vReactionSummary)
                && vReactionSummary.TryGetProperty("total_count", out var vReactionTotal)
                && vReactionTotal.ValueKind == JsonValueKind.Number)
            {
                reactionCount = vReactionTotal.GetInt32();
            }

            var commentCount = 0;
            if (it.TryGetProperty("comments", out var vComments)
                && vComments.TryGetProperty("summary", out var vCommentSummary)
                && vCommentSummary.TryGetProperty("total_count", out var vCommentTotal)
                && vCommentTotal.ValueKind == JsonValueKind.Number)
            {
                commentCount = vCommentTotal.GetInt32();
            }

            var shareCount = 0;
            if (it.TryGetProperty("shares", out var vShares)
                && vShares.TryGetProperty("count", out var vShareCount)
                && vShareCount.ValueKind == JsonValueKind.Number)
            {
                shareCount = vShareCount.GetInt32();
            }

            string? pictureUrl = null;

            if (it.TryGetProperty("full_picture", out var vFullPicture) && vFullPicture.ValueKind == JsonValueKind.String)
            {
                pictureUrl = vFullPicture.GetString();
            }

            if (it.TryGetProperty("attachments", out var vAttachments)
                && vAttachments.TryGetProperty("data", out var vAttData)
                && vAttData.ValueKind == JsonValueKind.Array)
            {
                foreach (var att in vAttData.EnumerateArray())
                {
                    if (att.TryGetProperty("media", out var vMedia)
                        && vMedia.TryGetProperty("image", out var vImage)
                        && vImage.TryGetProperty("src", out var vSrc)
                        && vSrc.ValueKind == JsonValueKind.String)
                    {
                        if (string.IsNullOrWhiteSpace(pictureUrl)) pictureUrl = vSrc.GetString();
                        if (!string.IsNullOrWhiteSpace(pictureUrl)) break;
                    }
                }
            }

            var viewCount = 0;

            list.Add(new PostItem
            {
                Id = id,
                Message = msg,
                PermalinkUrl = permalink,
                CreatedTime = created,
                PictureUrl = pictureUrl,
                LikeCount = reactionCount,
                CommentCount = commentCount,
                ShareCount = shareCount,
                ViewCount = viewCount,
            });
        }

        return list;
    }

    public class MyPageItem
    {
        public string PageId { get; set; } = string.Empty;
        public string PageName { get; set; } = string.Empty;
        public string? PictureUrl { get; set; }
        public string PageAccessToken { get; set; } = string.Empty;
    }

    public class PostItem
    {
        public string Id { get; set; } = string.Empty;
        public string? Message { get; set; }
        public DateTime? CreatedTime { get; set; }
        public string? PermalinkUrl { get; set; }
        public string? PictureUrl { get; set; }
        public int LikeCount { get; set; }
        public int CommentCount { get; set; }
        public int ShareCount { get; set; }
        public int ViewCount { get; set; }
    }
}
