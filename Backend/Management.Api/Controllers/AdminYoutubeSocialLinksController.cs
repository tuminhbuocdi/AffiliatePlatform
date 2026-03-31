using System.Security.Claims;
using System.Text.RegularExpressions;
using Management.Domain.Entities;
using Management.Infrastructure.External;
using Management.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Management.Api.Controllers;

[ApiController]
[Route("api/admin/youtube-social-links")]
[Authorize(Roles = "admin")]
public class AdminYoutubeSocialLinksController : ControllerBase
{
    private readonly YoutubeChannelRepository _ytChannels;
    private readonly YoutubeApiClient _yt;
    private readonly ProductAffiliateLinkRepository _productAffiliateLinks;
    private readonly ProductSocialLinkRepository _productSocialLinks;
    private readonly IHttpClientFactory _httpClientFactory;

    public AdminYoutubeSocialLinksController(
        YoutubeChannelRepository ytChannels,
        YoutubeApiClient yt,
        ProductAffiliateLinkRepository productAffiliateLinks,
        ProductSocialLinkRepository productSocialLinks,
        IHttpClientFactory httpClientFactory)
    {
        _ytChannels = ytChannels;
        _yt = yt;
        _productAffiliateLinks = productAffiliateLinks;
        _productSocialLinks = productSocialLinks;
        _httpClientFactory = httpClientFactory;
    }

    public class ScanRequest
    {
        public string? Query { get; set; }
        public int VideoLimitPerChannel { get; set; } = 200;
    }

    public class ScanRow
    {
        public string ChannelId { get; set; } = string.Empty;
        public string VideoId { get; set; } = string.Empty;
        public string VideoTitle { get; set; } = string.Empty;
        public string? PublishedAt { get; set; }

        public string ProductLink { get; set; } = string.Empty;
        public string AffiliateLink { get; set; } = string.Empty;
        public string ShortLink { get; set; } = string.Empty;
        public string SocialLink { get; set; } = string.Empty;

        public bool ExistsInDb { get; set; }
    }

    public class CollectRequest
    {
        public ScanRow[] Items { get; set; } = Array.Empty<ScanRow>();
    }

    public class CreateAffiliateLinkRequest
    {
        public string ShortLink { get; set; } = string.Empty;
    }

    public class CreateAffiliateLinkResponse
    {
        public bool Success { get; set; }
        public string ExternalItemId { get; set; } = string.Empty;
        public string ProductLink { get; set; } = string.Empty;
        public string AffiliateLink { get; set; } = string.Empty;
    }

    public class CollectExistingRequest
    {
        public CollectExistingItem[] Items { get; set; } = Array.Empty<CollectExistingItem>();
    }

    public class CollectExistingItem
    {
        public string ShortLink { get; set; } = string.Empty;
        public string SocialLink { get; set; } = string.Empty;
    }

    public class RefreshExistingRequest
    {
        public CollectExistingItem[] Items { get; set; } = Array.Empty<CollectExistingItem>();
        public string SocialLink { get; set; } = string.Empty;
    }

    [HttpPost("scan")]
    public async Task<IActionResult> Scan([FromBody] ScanRequest req)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var channels = await _ytChannels.GetByUserId(userId.Value);
        if (channels.Count == 0) return Ok(Array.Empty<ScanRow>());

        var result = new List<ScanRow>();
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var ch in channels)
        {
            if (string.IsNullOrWhiteSpace(ch.ChannelId)) continue;

            var conn = await _yt.GetAuthorizedConnection(userId.Value, ch.ChannelId);
            var videos = await _yt.ListChannelVideosAll(conn.AccessToken, ch.ChannelId, req.Query, req.VideoLimitPerChannel);

            foreach (var v in videos)
            {
                var text = (v.Title ?? string.Empty) + "\n" + (v.Description ?? string.Empty);
                var shortLinks = ExtractShopeeShortLinks(text);
                if (shortLinks.Count == 0) continue;

                foreach (var u in shortLinks)
                {
                    var key = $"{ch.ChannelId}||{v.Id}||{u}";
                    if (!unique.Add(key)) continue;

                    result.Add(new ScanRow
                    {
                        ChannelId = ch.ChannelId,
                        VideoId = v.Id,
                        VideoTitle = v.Title,
                        PublishedAt = v.PublishedAt,
                        ProductLink = string.Empty,
                        AffiliateLink = u,
                        ShortLink = u,
                        SocialLink = $"https://www.youtube.com/watch?v={v.Id}",
                    });
                }
            }
        }

        var existing = await _productAffiliateLinks.GetExistingShortLinksAsync(result.Select(x => x.ShortLink));
        foreach (var r in result)
        {
            r.ExistsInDb = existing.Contains(r.ShortLink);
        }

        return Ok(result);
    }

    [HttpPost("collect")]
    public async Task<IActionResult> Collect([FromBody] CollectRequest req)
    {
        if (req.Items == null || req.Items.Length == 0) return BadRequest("Items is required.");

        var now = DateTime.UtcNow;
        var items = req.Items
            .Where(x => !string.IsNullOrWhiteSpace(x.ProductLink)
                && !string.IsNullOrWhiteSpace(x.AffiliateLink)
                && !string.IsNullOrWhiteSpace(x.SocialLink))
            .Select(x => new ProductSocialLink
            {
                ProductLink = x.ProductLink.Trim(),
                AffiliateLink = x.AffiliateLink.Trim(),
                SocialLink = x.SocialLink.Trim(),
                CreatedAt = now,
            })
            .ToList();

        if (items.Count == 0) return BadRequest("No valid items.");

        var inserted = await _productSocialLinks.BulkInsertIgnoreDuplicatesAsync(items);
        return Ok(new { success = true, inserted });
    }

    [HttpPost("collect-existing")]
    public async Task<IActionResult> CollectExisting([FromBody] CollectExistingRequest req)
    {
        if (req?.Items == null || req.Items.Length == 0) return BadRequest("Items is required.");

        var input = req.Items
            .Where(x => !string.IsNullOrWhiteSpace(x.ShortLink) && !string.IsNullOrWhiteSpace(x.SocialLink))
            .Select(x => new
            {
                ShortLink = x.ShortLink.Trim(),
                SocialLink = x.SocialLink.Trim(),
            })
            .ToList();

        if (input.Count == 0) return BadRequest("No valid items.");

        var links = input.Select(x => x.ShortLink).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var mappings = await _productAffiliateLinks.GetByAffiliateLinksAsync(links);
        var map = new Dictionary<string, ProductAffiliateLink>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in mappings)
        {
            var key = (m.AffiliateLink ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(key)) continue;
            if (!map.ContainsKey(key)) map[key] = m;
        }

        var now = DateTime.UtcNow;
        var toInsert = new List<ProductSocialLink>();
        var missing = 0;

        foreach (var it in input)
        {
            if (!map.TryGetValue(it.ShortLink, out var m) || string.IsNullOrWhiteSpace(m.ProductLink) || string.IsNullOrWhiteSpace(m.AffiliateLink))
            {
                missing += 1;
                continue;
            }

            toInsert.Add(new ProductSocialLink
            {
                ProductLink = m.ProductLink.Trim(),
                AffiliateLink = (m.AffiliateLink ?? string.Empty).Trim(),
                SocialLink = it.SocialLink,
                CreatedAt = now,
            });
        }

        if (toInsert.Count == 0) return Ok(new { success = true, inserted = 0, missing });

        var inserted = await _productSocialLinks.BulkInsertIgnoreDuplicatesAsync(toInsert);
        return Ok(new { success = true, inserted, missing });
    }

    [HttpPost("refresh-existing")]
    public async Task<IActionResult> RefreshExisting([FromBody] RefreshExistingRequest req)
    {
        var socialLink = (req?.SocialLink ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(socialLink)) return BadRequest("SocialLink is required.");
        if (!IsYoutubeUrl(socialLink)) return BadRequest("SocialLink must be a YouTube URL.");

        var input = (req?.Items ?? Array.Empty<CollectExistingItem>())
            .Where(x => !string.IsNullOrWhiteSpace(x.ShortLink))
            .Select(x => new
            {
                ShortLink = x.ShortLink.Trim(),
                SocialLink = socialLink,
            })
            .DistinctBy(x => x.ShortLink, StringComparer.OrdinalIgnoreCase)
            .ToList();

        await _productSocialLinks.DeleteBySocialLinkAsync(socialLink);

        if (input.Count == 0) return Ok(new { success = true, deleted = true, inserted = 0, missing = 0 });

        var links = input.Select(x => x.ShortLink).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var mappings = await _productAffiliateLinks.GetByAffiliateLinksAsync(links);
        var map = new Dictionary<string, ProductAffiliateLink>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in mappings)
        {
            var affiliateLink = (m.AffiliateLink ?? string.Empty).Trim();
            var productLink = (m.ProductLink ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(affiliateLink) || string.IsNullOrWhiteSpace(productLink)) continue;
            if (!map.ContainsKey(affiliateLink)) map[affiliateLink] = m;
        }

        var now = DateTime.UtcNow;
        var toInsert = new List<ProductSocialLink>();
        var missing = 0;
        foreach (var it in input)
        {
            if (!map.TryGetValue(it.ShortLink, out var m))
            {
                missing += 1;
                continue;
            }

            var productLink = (m.ProductLink ?? string.Empty).Trim();
            var affiliateLink = (m.AffiliateLink ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(productLink) || string.IsNullOrWhiteSpace(affiliateLink))
            {
                missing += 1;
                continue;
            }

            toInsert.Add(new ProductSocialLink
            {
                ProductLink = productLink,
                AffiliateLink = affiliateLink,
                SocialLink = socialLink,
                CreatedAt = now,
            });
        }

        var inserted = toInsert.Count == 0 ? 0 : await _productSocialLinks.BulkInsertIgnoreDuplicatesAsync(toInsert);
        return Ok(new { success = true, inserted, missing });
    }

    [HttpPost("create-affiliate-link")]
    public async Task<IActionResult> CreateAffiliateLink([FromBody] CreateAffiliateLinkRequest req)
    {
        var shortLink = (req?.ShortLink ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(shortLink)) return BadRequest("ShortLink is required.");

        if (!Uri.TryCreate(shortLink, UriKind.Absolute, out var shortUri))
            return BadRequest("ShortLink is invalid.");
        if (!string.Equals(shortUri.Host, "s.shopee.vn", StringComparison.OrdinalIgnoreCase))
            return BadRequest("ShortLink host must be s.shopee.vn");

        var productLink = await ResolveShopeeShortLinkAsync(shortUri);
        productLink = NormalizeShopeeProductLink(productLink);
        var externalItemId = TryExtractShopeeItemId(productLink);
        if (string.IsNullOrWhiteSpace(externalItemId))
            return BadRequest($"Cannot extract item id from resolved url: {productLink}");

        var now = DateTime.UtcNow;
        await _productAffiliateLinks.UpsertAsync(new ProductAffiliateLink
        {
            ExternalItemId = externalItemId,
            ProductLink = productLink,
            AffiliateLink = shortLink,
            CreatedAt = now,
            UpdatedAt = now,
        });

        return Ok(new CreateAffiliateLinkResponse
        {
            Success = true,
            ExternalItemId = externalItemId,
            ProductLink = productLink,
            AffiliateLink = shortLink,
        });
    }

    private static List<string> ExtractShopeeShortLinks(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new List<string>();

        var matches = Regex.Matches(text, "https?://s\\.shopee\\.vn/[^\\s<>\"\\)\\]]+", RegexOptions.IgnoreCase);
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match m in matches)
        {
            var u = (m.Value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(u)) continue;

            u = u.TrimEnd('.', ',', ';', ':', '!', '?');
            if (string.IsNullOrWhiteSpace(u)) continue;

            if (!Uri.TryCreate(u, UriKind.Absolute, out var uri)) continue;
            if (!string.Equals(uri.Host, "s.shopee.vn", StringComparison.OrdinalIgnoreCase)) continue;

            set.Add(u);
        }

        return set.ToList();
    }

    private static bool UrlStartsWith(string url, string prefix)
    {
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(prefix)) return false;
        var a = url.Trim();
        var p = prefix.Trim();

        if (a.EndsWith("/", StringComparison.Ordinal)) a = a.TrimEnd('/');
        if (p.EndsWith("/", StringComparison.Ordinal)) p = p.TrimEnd('/');

        return a.StartsWith(p, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsYoutubeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        var host = (uri.Host ?? string.Empty).ToLowerInvariant();
        return host.Contains("youtube.com") || host.Contains("youtu.be");
    }

    private static string? TryExtractShopeeItemId(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        // common format: ...-i.<shopid>.<itemid>
        var m = Regex.Match(url, @"i\.(\d+)\.(\d+)", RegexOptions.IgnoreCase);
        if (m.Success && m.Groups.Count >= 3) return m.Groups[2].Value;

        // fallback: itemid query parameter
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            // /product/{shopid}/{itemid}
            var path = (uri.AbsolutePath ?? string.Empty).Trim('/');
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3
                && (string.Equals(parts[0], "product", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(parts[0], "opaanlp", StringComparison.OrdinalIgnoreCase))
                && long.TryParse(parts[1], out _)
                && long.TryParse(parts[2], out _))
            {
                return parts[2];
            }

            var q = uri.Query ?? string.Empty;
            var m2 = Regex.Match(q, @"(?:\?|&)(?:itemid|item_id)=(\d+)", RegexOptions.IgnoreCase);
            if (m2.Success && m2.Groups.Count >= 2) return m2.Groups[1].Value;
        }

        return null;
    }

    private static string NormalizeShopeeProductLink(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return url;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return url;

        // Strip query + fragment
        var clean = new UriBuilder(uri) { Query = string.Empty, Fragment = string.Empty };

        // Convert /opaanlp/{shopId}/{itemId} -> /product/{shopId}/{itemId}
        var path = (clean.Path ?? string.Empty).Trim('/');
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3
            && string.Equals(parts[0], "opaanlp", StringComparison.OrdinalIgnoreCase)
            && long.TryParse(parts[1], out _)
            && long.TryParse(parts[2], out _))
        {
            clean.Path = $"/product/{parts[1]}/{parts[2]}";
        }

        return clean.Uri.ToString();
    }

    private static async Task<string> ResolveShopeeShortLinkAsync(Uri shortUri)
    {
        // We manually follow redirects because the chain can be:
        // s.shopee.vn -> shope.ee -> shopee.vn/universal-link -> shopee.vn/product/...
        // and Shopee may block bot-like requests (redirecting to shope.ee/error_page).

        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
        };

        using var client = new HttpClient(handler);
        client.Timeout = TimeSpan.FromSeconds(15);

        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.Accept.ParseAdd(
            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("vi-VN,vi;q=0.9,en-US;q=0.8");

        Uri current = shortUri;
        for (var i = 0; i < 8; i++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, current);
            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);

            // 3xx: follow Location
            if ((int)resp.StatusCode >= 300 && (int)resp.StatusCode < 400)
            {
                var loc = resp.Headers.Location;
                if (loc == null) throw new InvalidOperationException("Shopee redirect without Location header.");
                current = loc.IsAbsoluteUri ? loc : new Uri(current, loc);

                if (current.Host.Contains("shope.ee", StringComparison.OrdinalIgnoreCase)
                    && current.AbsolutePath.Contains("error_page", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Shopee redirected to error_page.");
                }
                continue;
            }

            // final URL reached
            var finalUrl = current.ToString();
            if (finalUrl.Contains("shope.ee/error_page", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Shopee returned error_page.");
            return finalUrl;
        }

        throw new InvalidOperationException("Too many redirects when resolving Shopee short link.");
    }

    private Guid? GetUserId()
    {
        var idStr = User.FindFirstValue("uid") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(idStr, out var userId)) return userId;
        return null;
    }
}
