using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Linq;
using Management.Domain.Entities;
using Management.Infrastructure.Repositories;
using Microsoft.Extensions.Caching.Memory;

namespace Management.Infrastructure.External;

public class YoutubeApiClient
{
    private readonly HttpClient _http;
    private readonly YoutubeOAuthService _oauth;
    private readonly YoutubeChannelRepository _repo;
    private readonly IMemoryCache _cache;

    public YoutubeApiClient(HttpClient http, YoutubeOAuthService oauth, YoutubeChannelRepository repo, IMemoryCache cache)
    {
        _http = http;
        _oauth = oauth;
        _repo = repo;
        _cache = cache;
    }

    public async Task<YoutubeChannelConnection> GetAuthorizedConnection(Guid userId, string channelId)
    {
        var conn = await _repo.GetByUserAndChannelId(userId, channelId);
        if (conn == null || conn.IsRevoked)
        {
            throw new InvalidOperationException("Channel is not connected");
        }

        if (!string.IsNullOrWhiteSpace(conn.AccessToken)
            && conn.AccessTokenExpiresAtUtc.HasValue
            && conn.AccessTokenExpiresAtUtc.Value.AddMinutes(-2) > DateTime.UtcNow)
        {
            return conn;
        }

        var refreshed = await _oauth.RefreshAccessToken(conn.RefreshToken);
        var accessToken = refreshed.Access_Token;
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException("Refresh token failed");
        }

        conn.AccessToken = accessToken;
        conn.AccessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(refreshed.Expires_In);
        conn.Scope = refreshed.Scope ?? conn.Scope;
        conn.UpdatedAtUtc = DateTime.UtcNow;
        await _repo.Upsert(conn);

        return conn;
    }

    public async Task<ChannelInfo> GetMyChannelInfo(string accessToken)
    {
        var channels = await GetMyChannels(accessToken);
        var first = channels.FirstOrDefault();
        if (first == null) throw new InvalidOperationException("No channel found");
        return first;
    }

    public async Task<IReadOnlyList<ChannelInfo>> GetMyChannels(string accessToken)
    {
        // For Brand Accounts, channels.list managedByMe=true can return 403 depending on
        // permissions/ownership. mine=true is the most reliable way to get the channel bound
        // to the current OAuth token.
        var mine = await ListChannels(accessToken, "mine=true");
        return mine.ToList();
    }

    private async Task<IReadOnlyList<ChannelInfo>> ListChannels(string accessToken, string query)
    {
        var url = $"https://www.googleapis.com/youtube/v3/channels?part=snippet&maxResults=50&{query}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"YouTube API {(int)res.StatusCode} (channels.list {query}): {body}");
        }

        var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("items", out var items)) return Array.Empty<ChannelInfo>(); 

        var list = new List<ChannelInfo>();
        foreach (var it in items.EnumerateArray())
        {
            var id = it.TryGetProperty("id", out var idEl) ? (idEl.GetString() ?? string.Empty) : string.Empty;
            if (!it.TryGetProperty("snippet", out var snippet)) continue;
            var title = snippet.TryGetProperty("title", out var t) ? (t.GetString() ?? string.Empty) : string.Empty;
                
            string? thumb = null;
            if (snippet.TryGetProperty("thumbnails", out var thumbs)
                && thumbs.TryGetProperty("default", out var def)
                && def.TryGetProperty("url", out var u))
            {
                thumb = u.GetString();
            }

            list.Add(new ChannelInfo { ChannelId = id, Title = title, ThumbnailUrl = thumb });
        }

        return list;
    }

    private static long? TryGetInt64(JsonElement el, string property)
    {
        if (el.ValueKind == JsonValueKind.Undefined || el.ValueKind == JsonValueKind.Null) return null;
        if (!el.TryGetProperty(property, out var p)) return null;
        var s = p.GetString();
        if (long.TryParse(s, out var v)) return v;
        return null;
    }

    public class VideoEditInfo
    {
        public string[] Tags { get; set; } = Array.Empty<string>();
        public string[] PlaylistIds { get; set; } = Array.Empty<string>();
    }

    public async Task<VideoEditInfo> GetVideoEditInfo(string accessToken, string channelId, string videoId)
    {
        var tags = await GetVideoTags(accessToken, videoId);

        // There is no direct API to list all playlists containing a given video.
        // Best-effort: list channel playlists and check membership by scanning the first page (maxResults=50) of playlistItems.
        var playlistIds = new List<string>();
        IReadOnlyList<PlaylistItem> pls;
        try
        {
            pls = await ListChannelPlaylists(accessToken, channelId);
        }
        catch
        {
            pls = Array.Empty<PlaylistItem>();
        }

        foreach (var p in pls)
        {
            try
            {
                var itemId = await FindPlaylistItemId(accessToken, p.Id, videoId);
                if (!string.IsNullOrWhiteSpace(itemId)) playlistIds.Add(p.Id);
            }
            catch
            {
                // ignore per-playlist errors
            }
        }

        return new VideoEditInfo
        {
            Tags = tags,
            PlaylistIds = playlistIds.ToArray(),
        };
    }

    public async Task<string[]> GetVideoTags(string accessToken, string videoId)
    {
        var url = "https://www.googleapis.com/youtube/v3/videos?part=snippet&id=" + Uri.EscapeDataString(videoId);
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"YouTube API {(int)res.StatusCode} (videos.list tags {videoId}): {body}");
        }

        var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("items", out var items) || items.GetArrayLength() == 0)
        {
            return Array.Empty<string>();
        }

        var first = items[0];
        if (!first.TryGetProperty("snippet", out var snippet)) return Array.Empty<string>();
        if (!snippet.TryGetProperty("tags", out var tagsEl) || tagsEl.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var list = new List<string>();
        foreach (var t in tagsEl.EnumerateArray())
        {
            var s = t.GetString();
            if (!string.IsNullOrWhiteSpace(s)) list.Add(s);
        }

        return list.ToArray();
    }

    public async Task<IReadOnlyList<VideoItem>> ListChannelVideos(string accessToken, string channelId, string? query)
    {
        var searchUrl = new StringBuilder();
        // NOTE:
        // - Using channelId=... with search.list typically returns only PUBLIC videos.
        // - To include private/unlisted videos owned by the current OAuth user, we must use forMine=true.
        //   (Requires OAuth scopes like https://www.googleapis.com/auth/youtube.readonly or broader.)
        // - forMine=true cannot be combined with channelId, so we filter by snippet.channelId client-side.
        searchUrl.Append("https://www.googleapis.com/youtube/v3/search?part=snippet&order=date&type=video&maxResults=25&forMine=true");
        if (!string.IsNullOrWhiteSpace(query))
        {
            searchUrl.Append("&q=").Append(Uri.EscapeDataString(query));
        }

        using var searchReq = new HttpRequestMessage(HttpMethod.Get, searchUrl.ToString());
        searchReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var searchRes = await _http.SendAsync(searchReq);
        var searchBody = await searchRes.Content.ReadAsStringAsync();
        if (!searchRes.IsSuccessStatusCode) throw new InvalidOperationException(searchBody);

        var searchDoc = JsonDocument.Parse(searchBody);
        var items = searchDoc.RootElement.GetProperty("items");

        var ids = new List<string>();
        foreach (var el in items.EnumerateArray())
        {
            if (!el.TryGetProperty("snippet", out var sn)) continue;
            var snChannelId = sn.TryGetProperty("channelId", out var cidEl) ? (cidEl.GetString() ?? string.Empty) : string.Empty;
            if (!string.Equals(snChannelId, channelId, StringComparison.OrdinalIgnoreCase)) continue;

            if (el.TryGetProperty("id", out var idEl)
                && idEl.TryGetProperty("videoId", out var vid))
            {
                var v = vid.GetString();
                if (!string.IsNullOrWhiteSpace(v)) ids.Add(v);
            }
        }

        if (ids.Count == 0) return Array.Empty<VideoItem>();

        var videosUrl = "https://www.googleapis.com/youtube/v3/videos?part=snippet,status,statistics&id=" + Uri.EscapeDataString(string.Join(',', ids));
        using var videosReq = new HttpRequestMessage(HttpMethod.Get, videosUrl);
        videosReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var videosRes = await _http.SendAsync(videosReq);
        var videosBody = await videosRes.Content.ReadAsStringAsync();
        if (!videosRes.IsSuccessStatusCode) throw new InvalidOperationException(videosBody);

        var videosDoc = JsonDocument.Parse(videosBody);
        var videoItems = videosDoc.RootElement.GetProperty("items");
        return ParseVideoItems(videoItems);
    }

    public async Task<IReadOnlyList<VideoItem>> ListChannelVideosAll(string accessToken, string channelId, string? query, int limit = 200)
    {
        var take = Math.Max(1, Math.Min(limit, 500));
        var ids = new List<string>();

        string? pageToken = null;
        for (var attempt = 0; attempt < 20 && ids.Count < take; attempt++)
        {
            var searchUrl = new StringBuilder();
            searchUrl.Append("https://www.googleapis.com/youtube/v3/search?part=snippet&order=date&type=video&maxResults=50&forMine=true");
            if (!string.IsNullOrWhiteSpace(query))
            {
                searchUrl.Append("&q=").Append(Uri.EscapeDataString(query));
            }
            if (!string.IsNullOrWhiteSpace(pageToken))
            {
                searchUrl.Append("&pageToken=").Append(Uri.EscapeDataString(pageToken));
            }

            using var searchReq = new HttpRequestMessage(HttpMethod.Get, searchUrl.ToString());
            searchReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var searchRes = await _http.SendAsync(searchReq);
            var searchBody = await searchRes.Content.ReadAsStringAsync();
            if (!searchRes.IsSuccessStatusCode) throw new InvalidOperationException(searchBody);

            var searchDoc = JsonDocument.Parse(searchBody);
            if (!searchDoc.RootElement.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            {
                break;
            }

            foreach (var el in items.EnumerateArray())
            {
                if (ids.Count >= take) break;
                if (!el.TryGetProperty("snippet", out var sn)) continue;
                var snChannelId = sn.TryGetProperty("channelId", out var cidEl) ? (cidEl.GetString() ?? string.Empty) : string.Empty;
                if (!string.Equals(snChannelId, channelId, StringComparison.OrdinalIgnoreCase)) continue;

                if (el.TryGetProperty("id", out var idEl)
                    && idEl.TryGetProperty("videoId", out var vid))
                {
                    var v = vid.GetString();
                    if (!string.IsNullOrWhiteSpace(v)) ids.Add(v);
                }
            }

            pageToken = searchDoc.RootElement.TryGetProperty("nextPageToken", out var npt) ? npt.GetString() : null;
            if (string.IsNullOrWhiteSpace(pageToken)) break;
        }

        if (ids.Count == 0) return Array.Empty<VideoItem>();

        var all = new List<VideoItem>();
        for (var i = 0; i < ids.Count; i += 50)
        {
            var batch = ids.Skip(i).Take(50).ToArray();
            var videosUrl = "https://www.googleapis.com/youtube/v3/videos?part=snippet,status,statistics&id=" + Uri.EscapeDataString(string.Join(',', batch));
            using var videosReq = new HttpRequestMessage(HttpMethod.Get, videosUrl);
            videosReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var videosRes = await _http.SendAsync(videosReq);
            var videosBody = await videosRes.Content.ReadAsStringAsync();
            if (!videosRes.IsSuccessStatusCode) throw new InvalidOperationException(videosBody);

            var videosDoc = JsonDocument.Parse(videosBody);
            if (!videosDoc.RootElement.TryGetProperty("items", out var videoItems) || videoItems.ValueKind != JsonValueKind.Array)
            {
                continue;
            }
            all.AddRange(ParseVideoItems(videoItems));
        }

        return all;
    }

    private static List<VideoItem> ParseVideoItems(JsonElement videoItems)
    {
        var list = new List<VideoItem>();
        foreach (var v in videoItems.EnumerateArray())
        {
            var id = v.GetProperty("id").GetString() ?? string.Empty;
            var snippet = v.GetProperty("snippet");
            var status = v.GetProperty("status");
            var statistics = v.TryGetProperty("statistics", out var st) ? st : default;

            var title = snippet.GetProperty("title").GetString() ?? string.Empty;
            var description = snippet.TryGetProperty("description", out var desc) ? desc.GetString() : null;
            var publishedAt = snippet.TryGetProperty("publishedAt", out var pub) ? pub.GetString() : null;

            string? thumb = null;
            if (snippet.TryGetProperty("thumbnails", out var thumbs)
                && thumbs.TryGetProperty("default", out var def)
                && def.TryGetProperty("url", out var u))
            {
                thumb = u.GetString();
            }

            var privacy = status.TryGetProperty("privacyStatus", out var ps) ? ps.GetString() ?? "" : "";

            var tags = new List<string>();
            if (snippet.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var x in tagsEl.EnumerateArray())
                {
                    var s = x.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) tags.Add(s);
                }
            }

            long? viewCount = TryGetInt64(statistics, "viewCount");
            long? likeCount = TryGetInt64(statistics, "likeCount");
            long? commentCount = TryGetInt64(statistics, "commentCount");
            long? dislikeCount = TryGetInt64(statistics, "dislikeCount");

            list.Add(new VideoItem
            {
                Id = id,
                Title = title,
                Description = description,
                ThumbnailUrl = thumb,
                PublishedAt = publishedAt,
                PrivacyStatus = privacy,
                ViewCount = viewCount,
                LikeCount = likeCount,
                DislikeCount = dislikeCount,
                CommentCount = commentCount,
                Tags = tags.ToArray(),
            });
        }

        return list;
    }

    public async Task UpdateVideo(string accessToken, string videoId, string title, string? description, string privacyStatus, string regionCode = "VN", IReadOnlyList<string>? tags = null, DateTime? publishAtUtc = null)
    {
        var categoryId = await GetDefaultCategoryIdAsync(accessToken, regionCode);
        if (description == null || tags == null)
        {
            var current = await GetVideoForUpdate(accessToken, videoId);
            if (description == null) description = current.Description;
            if (tags == null) tags = current.Tags;
        }

        var url = "https://www.googleapis.com/youtube/v3/videos?part=snippet,status";

        string[]? tagsArr = null;
        if (tags != null)
        {
            tagsArr = tags
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(500)
                .ToArray();
        }

        object status = publishAtUtc.HasValue
            ? new { privacyStatus, publishAt = publishAtUtc.Value.ToString("O") }
            : new { privacyStatus };

        var payload = new
        {
            id = videoId,
            snippet = new { title, description, categoryId, tags = tagsArr },
            status,
        };

        using var req = new HttpRequestMessage(HttpMethod.Put, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"YouTube API {(int)res.StatusCode} (videos.update {videoId}): {body}");
    }

    private async Task<(string Title, string? Description, string PrivacyStatus, string[] Tags)> GetVideoForUpdate(string accessToken, string videoId)
    {
        var url = "https://www.googleapis.com/youtube/v3/videos?part=snippet,status&id=" + Uri.EscapeDataString(videoId);
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"YouTube API {(int)res.StatusCode} (videos.list for update {videoId}): {body}");

        var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("items", out var items) || items.GetArrayLength() == 0)
        {
            throw new InvalidOperationException($"Video not found: {videoId}");
        }

        var first = items[0];
        var snippet = first.TryGetProperty("snippet", out var sn) ? sn : default;
        var status = first.TryGetProperty("status", out var st) ? st : default;

        var title = snippet.TryGetProperty("title", out var t) ? (t.GetString() ?? string.Empty) : string.Empty;
        var description = snippet.TryGetProperty("description", out var d) ? d.GetString() : null;
        var privacy = status.TryGetProperty("privacyStatus", out var ps) ? (ps.GetString() ?? "public") : "public";

        var tags = new List<string>();
        if (snippet.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var x in tagsEl.EnumerateArray())
            {
                var s = x.GetString();
                if (!string.IsNullOrWhiteSpace(s)) tags.Add(s);
            }
        }

        return (title, description, privacy, tags.ToArray());
    }

    public async Task SetVideoThumbnail(string accessToken, string videoId, Stream imageStream, long imageLength, string contentType)
    {
        var url = "https://www.googleapis.com/upload/youtube/v3/thumbnails/set?uploadType=media&videoId=" + Uri.EscapeDataString(videoId);

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var streamContent = new StreamContent(imageStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(contentType) ? "image/*" : contentType);
        streamContent.Headers.ContentLength = imageLength;
        req.Content = streamContent;

        var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"YouTube API {(int)res.StatusCode} (thumbnails.set {videoId}): {body}");
    }

    public class PlaylistItem
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
    }

    public async Task<string> CreatePlaylist(string accessToken, string channelId, string title, string? description, string privacyStatus)
    {
        var url = "https://www.googleapis.com/youtube/v3/playlists?part=snippet,status";
        var payload = new
        {
            snippet = new { title, description, channelId },
            status = new { privacyStatus },
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"YouTube API {(int)res.StatusCode} (playlists.insert {channelId}): {body}");

        var doc = JsonDocument.Parse(body);
        return doc.RootElement.TryGetProperty("id", out var idEl) ? (idEl.GetString() ?? string.Empty) : string.Empty;
    }

    public async Task UpdatePlaylist(string accessToken, string playlistId, string title, string? description, string privacyStatus)
    {
        var url = "https://www.googleapis.com/youtube/v3/playlists?part=snippet,status";
        var payload = new
        {
            id = playlistId,
            snippet = new { title, description },
            status = new { privacyStatus },
        };

        using var req = new HttpRequestMessage(HttpMethod.Put, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"YouTube API {(int)res.StatusCode} (playlists.update {playlistId}): {body}");
    }

    public async Task DeletePlaylist(string accessToken, string playlistId)
    {
        var url = "https://www.googleapis.com/youtube/v3/playlists?id=" + Uri.EscapeDataString(playlistId);
        using var req = new HttpRequestMessage(HttpMethod.Delete, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"YouTube API {(int)res.StatusCode} (playlists.delete {playlistId}): {body}");
    }

    public async Task<IReadOnlyList<PlaylistItem>> ListChannelPlaylists(string accessToken, string channelId)
    {
        var url = "https://www.googleapis.com/youtube/v3/playlists?part=snippet&maxResults=50&channelId=" + Uri.EscapeDataString(channelId);
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"YouTube API {(int)res.StatusCode} (playlists.list {channelId}): {body}");

        var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("items", out var items)) return Array.Empty<PlaylistItem>();

        var list = new List<PlaylistItem>();
        foreach (var it in items.EnumerateArray())
        {
            var id = it.TryGetProperty("id", out var idEl) ? (idEl.GetString() ?? string.Empty) : string.Empty;
            if (!it.TryGetProperty("snippet", out var snippet)) continue;
            var title = snippet.TryGetProperty("title", out var t) ? (t.GetString() ?? string.Empty) : string.Empty;
            if (string.IsNullOrWhiteSpace(id)) continue;
            list.Add(new PlaylistItem { Id = id, Title = title });
        }

        return list;
    }

    public async Task<string?> FindPlaylistItemId(string accessToken, string playlistId, string videoId)
    {
        var url = "https://www.googleapis.com/youtube/v3/playlistItems?part=snippet&maxResults=50&playlistId="
            + Uri.EscapeDataString(playlistId);

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"YouTube API {(int)res.StatusCode} (playlistItems.list {playlistId}): {body}");

        var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("items", out var items)) return null;

        foreach (var it in items.EnumerateArray())
        {
            var id = it.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            if (!it.TryGetProperty("snippet", out var snippet)) continue;
            if (!snippet.TryGetProperty("resourceId", out var resId)) continue;
            var kind = resId.TryGetProperty("kind", out var k) ? k.GetString() : null;
            var v = resId.TryGetProperty("videoId", out var vid) ? vid.GetString() : null;

            if (string.Equals(kind, "youtube#video", StringComparison.OrdinalIgnoreCase)
                && string.Equals(v, videoId, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(id))
            {
                return id;
            }
        }

        return null;
    }

    public async Task AddVideoToPlaylist(string accessToken, string playlistId, string videoId)
    {
        var url = "https://www.googleapis.com/youtube/v3/playlistItems?part=snippet";
        var payload = new
        {
            snippet = new
            {
                playlistId,
                resourceId = new { kind = "youtube#video", videoId }
            }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        req.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"YouTube API {(int)res.StatusCode} (playlistItems.insert {playlistId} {videoId}): {body}");
    }

    public async Task AddVideosToPlaylist(string accessToken, string playlistId, IReadOnlyList<string> videoIds)
    {
        foreach (var id in videoIds.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await AddVideoToPlaylist(accessToken, playlistId, id);
        }
    }

    public async Task BatchUpdateVideos(string accessToken, IReadOnlyList<string> videoIds, string? title, string? description, string? privacyStatus, string regionCode = "VN", IReadOnlyList<string>? tags = null)
    {
        foreach (var id in videoIds.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var current = await GetVideoForUpdate(accessToken, id);

            var nextTitle = title ?? current.Title;
            var nextDescription = description ?? current.Description;
            var nextPrivacy = privacyStatus ?? current.PrivacyStatus;

            IReadOnlyList<string>? nextTags = tags;
            if (tags == null)
            {
                nextTags = current.Tags;
            }

            if (string.IsNullOrWhiteSpace(nextTitle))
            {
                throw new InvalidOperationException($"Video title is empty: {id}");
            }

            await UpdateVideo(accessToken, id, nextTitle, nextDescription, nextPrivacy, regionCode, nextTags);
        }
    }

    public async Task RemoveVideoFromPlaylist(string accessToken, string playlistId, string videoId)
    {
        var itemId = await FindPlaylistItemId(accessToken, playlistId, videoId);
        if (string.IsNullOrWhiteSpace(itemId)) return;

        var url = "https://www.googleapis.com/youtube/v3/playlistItems?id=" + Uri.EscapeDataString(itemId);
        using var req = new HttpRequestMessage(HttpMethod.Delete, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"YouTube API {(int)res.StatusCode} (playlistItems.delete {playlistId} {videoId}): {body}");
    }

    private async Task<string> GetDefaultCategoryIdAsync(string accessToken, string regionCode)
    {
        const string cacheKeyPrefix = "yt_video_categories_";
        var cacheKey = $"{cacheKeyPrefix}{regionCode}";

        if (_cache.TryGetValue(cacheKey, out List<VideoCategory>? cached) && cached != null)
        {
            var first = cached.FirstOrDefault(c => c.Assignable);
            return first?.Id?.ToString() ?? "22"; // fallback to People & Blogs
        }

        var url = $"https://www.googleapis.com/youtube/v3/videoCategories?part=snippet&regionCode={regionCode}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) return "22"; // fallback

        var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("items", out var items)) return "22";

        var list = new List<VideoCategory>();
        foreach (var it in items.EnumerateArray())
        {
            var id = it.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            var snippet = it.TryGetProperty("snippet", out var snip) ? snip : default;
            var assignable = snippet.TryGetProperty("assignable", out var ass) == true && ass.GetBoolean() == true;
            var title = snippet.TryGetProperty("title", out var t) == true ? t.GetString() : null;

            if (!string.IsNullOrWhiteSpace(id) && assignable)
            {
                list.Add(new VideoCategory { Id = id, Title = title ?? string.Empty, Assignable = assignable });
            }
        }

        _cache.Set(cacheKey, list, TimeSpan.FromHours(24));

        var firstAssignable = list.FirstOrDefault();
        return firstAssignable?.Id ?? "22";
    }

    private class VideoCategory
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public bool Assignable { get; set; }
    }

    public async Task DeleteVideo(string accessToken, string videoId)
    {
        var url = "https://www.googleapis.com/youtube/v3/videos?id=" + Uri.EscapeDataString(videoId);
        using var req = new HttpRequestMessage(HttpMethod.Delete, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException(body);
    }

    public async Task<string> UploadVideoResumable(
        string accessToken,
        Stream fileStream,
        long fileLength,
        string title,
        string? description,
        string privacyStatus,
        IReadOnlyList<string>? tags = null,
        DateTime? publishAtUtc = null)
    {
        var initUrl = "https://www.googleapis.com/upload/youtube/v3/videos?uploadType=resumable&part=snippet,status";

        string[]? tagsArr = null;
        if (tags != null)
        {
            tagsArr = tags
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(500)
                .ToArray();
        }

        object status = publishAtUtc.HasValue
            ? new { privacyStatus, publishAt = publishAtUtc.Value.ToString("O") }
            : new { privacyStatus };

        var initPayload = new
        {
            snippet = new { title, description, tags = tagsArr },
            status,
        };

        using var initReq = new HttpRequestMessage(HttpMethod.Post, initUrl);
        initReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        initReq.Headers.Add("X-Upload-Content-Type", "video/*");
        initReq.Headers.Add("X-Upload-Content-Length", fileLength.ToString());
        initReq.Content = new StringContent(JsonSerializer.Serialize(initPayload), Encoding.UTF8, "application/json");

        var initRes = await _http.SendAsync(initReq);
        var initBody = await initRes.Content.ReadAsStringAsync();
        if (!initRes.IsSuccessStatusCode) throw new InvalidOperationException(initBody);

        var uploadUrl = initRes.Headers.Location?.ToString();
        if (string.IsNullOrWhiteSpace(uploadUrl)) throw new InvalidOperationException("Missing resumable upload location");

        using var putReq = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
        putReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        putReq.Content = new StreamContent(fileStream);
        putReq.Content.Headers.ContentType = new MediaTypeHeaderValue("video/*");
        putReq.Content.Headers.ContentLength = fileLength;
        putReq.Content.Headers.Add("Content-Range", $"bytes 0-{fileLength - 1}/{fileLength}");

        var putRes = await _http.SendAsync(putReq);
        var putBody = await putRes.Content.ReadAsStringAsync();
        if (!putRes.IsSuccessStatusCode) throw new InvalidOperationException(putBody);

        var doc = JsonDocument.Parse(putBody);
        return doc.RootElement.TryGetProperty("id", out var idEl) ? (idEl.GetString() ?? string.Empty) : string.Empty;
    }

    public async Task<AnalyticsSummary> GetAnalyticsSummary(string accessToken, string channelId, DateOnly startDate, DateOnly endDate)
    {
        var url = new StringBuilder();
        url.Append("https://youtubeanalytics.googleapis.com/v2/reports?");
        url.Append("ids=").Append(Uri.EscapeDataString("channel==" + channelId));
        url.Append("&startDate=").Append(Uri.EscapeDataString(startDate.ToString("yyyy-MM-dd")));
        url.Append("&endDate=").Append(Uri.EscapeDataString(endDate.ToString("yyyy-MM-dd")));
        url.Append("&metrics=").Append(Uri.EscapeDataString("estimatedMinutesWatched,views,averageViewDuration,subscribersGained"));

        using var req = new HttpRequestMessage(HttpMethod.Get, url.ToString());
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException(body);

        var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("rows", out var rows) || rows.GetArrayLength() == 0)
        {
            return new AnalyticsSummary();
        }

        var row = rows[0];
        return new AnalyticsSummary
        {
            WatchTimeMinutes = row[0].GetDouble(),
            Views = row[1].GetDouble(),
            AvgViewDurationSec = row[2].GetDouble(),
            SubsGained = row[3].GetDouble(),
        };
    }

    public class ChannelInfo
    {
        public string ChannelId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? ThumbnailUrl { get; set; }
    }

    public class VideoItem
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ThumbnailUrl { get; set; }
        public string? PublishedAt { get; set; }
        public string PrivacyStatus { get; set; } = string.Empty;
        public long? ViewCount { get; set; }
        public long? CommentCount { get; set; }
        public long? LikeCount { get; set; }
        public long? DislikeCount { get; set; }
        public string[] Tags { get; set; } = Array.Empty<string>();
    }

    public class AnalyticsSummary
    {
        public double? WatchTimeMinutes { get; set; }
        public double? Views { get; set; }
        public double? AvgViewDurationSec { get; set; }
        public double? SubsGained { get; set; }
    }
}
