using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace Management.Infrastructure.External;

public class FacebookOAuthService
{
    private readonly IConfiguration _config;
    private readonly IMemoryCache _cache;
    private readonly HttpClient _http;

    public FacebookOAuthService(IConfiguration config, IMemoryCache cache, HttpClient http)
    {
        _config = config;
        _cache = cache;
        _http = http;
    }

    public string[] GetScopes() => new[]
    {
        "pages_show_list",
        "pages_manage_posts",
        "pages_read_engagement",
        "read_insights",
    };

    public string GetAuthorizationUrl(Guid userId)
    {
        var appId = _config["Facebook:AppId"];
        var redirectUri = _config["Facebook:RedirectUri"];
        var graphVersion = _config["Facebook:GraphApiVersion"] ?? "v20.0";

        if (string.IsNullOrWhiteSpace(appId)) throw new InvalidOperationException("Missing configuration: Facebook:AppId");
        if (string.IsNullOrWhiteSpace(redirectUri)) throw new InvalidOperationException("Missing configuration: Facebook:RedirectUri");

        var state = CreateState();
        _cache.Set(GetStateCacheKey(state), userId, TimeSpan.FromMinutes(10));

        var scope = string.Join(',', GetScopes());

        var url = new StringBuilder();
        url.Append("https://www.facebook.com/").Append(graphVersion).Append("/dialog/oauth?");
        url.Append("client_id=").Append(Uri.EscapeDataString(appId));
        url.Append("&redirect_uri=").Append(Uri.EscapeDataString(redirectUri));
        url.Append("&response_type=code");
        url.Append("&scope=").Append(Uri.EscapeDataString(scope));
        url.Append("&state=").Append(Uri.EscapeDataString(state));
        url.Append("&auth_type=rerequest");

        return url.ToString();
    }

    public Guid ValidateStateAndGetUserId(string state)
    {
        if (string.IsNullOrWhiteSpace(state)) throw new InvalidOperationException("Missing state");

        if (_cache.TryGetValue<Guid>(GetStateCacheKey(state), out var userId))
        {
            _cache.Remove(GetStateCacheKey(state));
            return userId;
        }

        throw new InvalidOperationException("Invalid or expired state");
    }

    public async Task<TokenResponse> ExchangeCodeForToken(string code)
    {
        var appId = _config["Facebook:AppId"];
        var appSecret = _config["Facebook:AppSecret"];
        var redirectUri = _config["Facebook:RedirectUri"];
        var graphVersion = _config["Facebook:GraphApiVersion"] ?? "v20.0";

        if (string.IsNullOrWhiteSpace(appId)) throw new InvalidOperationException("Missing configuration: Facebook:AppId");
        if (string.IsNullOrWhiteSpace(appSecret)) throw new InvalidOperationException("Missing configuration: Facebook:AppSecret");
        if (string.IsNullOrWhiteSpace(redirectUri)) throw new InvalidOperationException("Missing configuration: Facebook:RedirectUri");

        var url = new StringBuilder();
        url.Append("https://graph.facebook.com/").Append(graphVersion).Append("/oauth/access_token?");
        url.Append("client_id=").Append(Uri.EscapeDataString(appId));
        url.Append("&redirect_uri=").Append(Uri.EscapeDataString(redirectUri));
        url.Append("&client_secret=").Append(Uri.EscapeDataString(appSecret));
        url.Append("&code=").Append(Uri.EscapeDataString(code));

        using var req = new HttpRequestMessage(HttpMethod.Get, url.ToString());
        var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(body);
        }

        var token = JsonSerializer.Deserialize<TokenResponse>(body, JsonOptions())
            ?? throw new InvalidOperationException("Invalid token response");

        return token;
    }

    public async Task<TokenResponse> ExchangeForLongLivedToken(string shortLivedAccessToken)
    {
        var appId = _config["Facebook:AppId"];
        var appSecret = _config["Facebook:AppSecret"];
        var graphVersion = _config["Facebook:GraphApiVersion"] ?? "v20.0";

        if (string.IsNullOrWhiteSpace(appId)) throw new InvalidOperationException("Missing configuration: Facebook:AppId");
        if (string.IsNullOrWhiteSpace(appSecret)) throw new InvalidOperationException("Missing configuration: Facebook:AppSecret");

        var url = new StringBuilder();
        url.Append("https://graph.facebook.com/").Append(graphVersion).Append("/oauth/access_token?");
        url.Append("grant_type=fb_exchange_token");
        url.Append("&client_id=").Append(Uri.EscapeDataString(appId));
        url.Append("&client_secret=").Append(Uri.EscapeDataString(appSecret));
        url.Append("&fb_exchange_token=").Append(Uri.EscapeDataString(shortLivedAccessToken));

        using var req = new HttpRequestMessage(HttpMethod.Get, url.ToString());
        var res = await _http.SendAsync(req);
        var body = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(body);
        }

        var token = JsonSerializer.Deserialize<TokenResponse>(body, JsonOptions())
            ?? throw new InvalidOperationException("Invalid token response");

        return token;
    }

    private static string CreateState()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string GetStateCacheKey(string state) => $"fb_oauth_state:{state}";

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public class TokenResponse
    {
        public string? Access_Token { get; set; }
        public string? Token_Type { get; set; }
        public int Expires_In { get; set; }
    }
}
