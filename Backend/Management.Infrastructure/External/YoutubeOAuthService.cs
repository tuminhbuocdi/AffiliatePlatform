using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace Management.Infrastructure.External;

public class YoutubeOAuthService
{
    private readonly IConfiguration _config;
    private readonly IMemoryCache _cache;
    private readonly HttpClient _http;

    public YoutubeOAuthService(IConfiguration config, IMemoryCache cache, HttpClient http)
    {
        _config = config;
        _cache = cache;
        _http = http;
    }

    public string[] GetScopes() => new[]
    {
        "https://www.googleapis.com/auth/youtube.upload",
        "https://www.googleapis.com/auth/youtube",
        "https://www.googleapis.com/auth/yt-analytics.readonly",
    };

    public string GetAuthorizationUrl(Guid userId)
    {
        var clientId = _config["Google:ClientId"];
        var redirectUri = _config["Google:RedirectUri"];

        if (string.IsNullOrWhiteSpace(clientId)) throw new InvalidOperationException("Missing configuration: Google:ClientId");
        if (string.IsNullOrWhiteSpace(redirectUri)) throw new InvalidOperationException("Missing configuration: Google:RedirectUri");

        var state = CreateState();
        _cache.Set(GetStateCacheKey(state), userId, TimeSpan.FromMinutes(10));

        var scope = string.Join(' ', GetScopes());

        var url = new StringBuilder();
        url.Append("https://accounts.google.com/o/oauth2/v2/auth?");
        url.Append("client_id=").Append(Uri.EscapeDataString(clientId));
        url.Append("&redirect_uri=").Append(Uri.EscapeDataString(redirectUri));
        url.Append("&response_type=code");
        url.Append("&access_type=offline");
        url.Append("&prompt=consent");
        url.Append("&include_granted_scopes=true");
        url.Append("&scope=").Append(Uri.EscapeDataString(scope));
        url.Append("&state=").Append(Uri.EscapeDataString(state));

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
        var clientId = _config["Google:ClientId"];
        var clientSecret = _config["Google:ClientSecret"];
        var redirectUri = _config["Google:RedirectUri"];

        if (string.IsNullOrWhiteSpace(clientId)) throw new InvalidOperationException("Missing configuration: Google:ClientId");
        if (string.IsNullOrWhiteSpace(clientSecret)) throw new InvalidOperationException("Missing configuration: Google:ClientSecret");
        if (string.IsNullOrWhiteSpace(redirectUri)) throw new InvalidOperationException("Missing configuration: Google:RedirectUri");

        var form = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = redirectUri,
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
        {
            Content = new FormUrlEncodedContent(form),
        };

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

    public async Task<TokenResponse> RefreshAccessToken(string refreshToken)
    {
        var clientId = _config["Google:ClientId"];
        var clientSecret = _config["Google:ClientSecret"];

        if (string.IsNullOrWhiteSpace(clientId)) throw new InvalidOperationException("Missing configuration: Google:ClientId");
        if (string.IsNullOrWhiteSpace(clientSecret)) throw new InvalidOperationException("Missing configuration: Google:ClientSecret");

        var form = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token",
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
        {
            Content = new FormUrlEncodedContent(form),
        };

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

    private static string GetStateCacheKey(string state) => $"yt_oauth_state:{state}";

    private static JsonSerializerOptions JsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public class TokenResponse
    {
        public string? Access_Token { get; set; }
        public int Expires_In { get; set; }
        public string? Refresh_Token { get; set; }
        public string? Scope { get; set; }
        public string? Token_Type { get; set; }
    }
}
