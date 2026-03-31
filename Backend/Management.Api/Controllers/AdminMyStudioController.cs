using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Management.Api.Controllers;

[ApiController]
[Route("api/admin/my-studio")]
[Authorize(Roles = "admin")]
public class AdminMyStudioController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;

    public AdminMyStudioController(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
    }

    [HttpPost("whisper")]
    [RequestSizeLimit(500_000_000)]
    public async Task<IActionResult> Whisper([FromForm] IFormFile file, [FromForm] string? format, [FromForm] string? language)
    {
        if (file == null || file.Length <= 0) return BadRequest("File is required");

        var apiKey = (_config["OpenAI:ApiKey"] ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return StatusCode(500, "Missing config: OpenAI:ApiKey");
        }

        var fmt = (format ?? "srt").Trim().ToLowerInvariant();
        if (fmt is not ("srt" or "vtt" or "json")) return BadRequest("format must be srt, vtt, or json");

        var lang = (language ?? string.Empty).Trim();
        if (lang.Length > 16) lang = lang[..16];

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(10);

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/transcriptions");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        using var formData = new MultipartFormDataContent();

        formData.Add(new StringContent("whisper-1"), "model");
        formData.Add(new StringContent(fmt), "response_format");
        if (!string.IsNullOrWhiteSpace(lang)) formData.Add(new StringContent(lang), "language");

        await using var fs = file.OpenReadStream();
        var fileContent = new StreamContent(fs);
        if (!string.IsNullOrWhiteSpace(file.ContentType))
        {
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
        }
        formData.Add(fileContent, "file", file.FileName ?? "input");

        req.Content = formData;

        using var res = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, HttpContext.RequestAborted);
        var bytes = await res.Content.ReadAsByteArrayAsync(HttpContext.RequestAborted);

        if (!res.IsSuccessStatusCode)
        {
            var body = bytes.Length > 0 ? System.Text.Encoding.UTF8.GetString(bytes) : "OpenAI Whisper error";
            return StatusCode((int)res.StatusCode, body);
        }

        var baseName = Path.GetFileNameWithoutExtension(file.FileName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "subtitle";

        if (fmt == "json")
        {
            return File(bytes, "application/json", baseName + ".json");
        }

        var contentType = fmt == "vtt" ? "text/vtt" : "application/x-subrip";
        return File(bytes, contentType, baseName + "." + fmt);
    }
}
