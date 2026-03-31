using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;

namespace Management.Api.Controllers
{
    [ApiController]
    [Route("api/admin/audio")]
    [Authorize(Roles = "admin")]
    public class AdminAudioController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;

        public AdminAudioController(IHttpClientFactory httpClientFactory, IConfiguration config)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
        }

        public class FreesoundSearchItem
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public double Duration { get; set; }
            public string? PreviewUrl { get; set; }
        }

        [HttpGet("freesound/search")]
        public async Task<IActionResult> FreesoundSearch([FromQuery] string q, [FromQuery] int page = 1, [FromQuery(Name = "pageSize")] int pageSize = 12)
        {
            q = (q ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(q)) return BadRequest("q is required");

            var apiKey = (_config["Freesound:ApiKey"] ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return StatusCode(500, "Missing config: Freesound:ApiKey");
            }

            page = Math.Max(1, page);
            pageSize = Math.Max(1, Math.Min(50, pageSize));

            var fields = "id,name,duration,previews";
            var url = $"https://freesound.org/apiv2/search/text/?query={WebUtility.UrlEncode(q)}&page={page}&page_size={pageSize}&fields={WebUtility.UrlEncode(fields)}&token={WebUtility.UrlEncode(apiKey)}";

            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);

            using var res = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, HttpContext.RequestAborted);
            var body = await res.Content.ReadAsStringAsync(HttpContext.RequestAborted);
            if (!res.IsSuccessStatusCode)
            {
                return StatusCode((int)res.StatusCode, string.IsNullOrWhiteSpace(body) ? "Freesound error" : body);
            }

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("results", out var resultsEl) || resultsEl.ValueKind != JsonValueKind.Array)
            {
                return Ok(new { count = 0, results = Array.Empty<FreesoundSearchItem>() });
            }

            var items = new List<FreesoundSearchItem>();
            foreach (var el in resultsEl.EnumerateArray())
            {
                var id = el.TryGetProperty("id", out var idEl) && idEl.TryGetInt32(out var idVal) ? idVal : 0;
                var name = el.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String ? nameEl.GetString() : null;
                var duration = el.TryGetProperty("duration", out var durEl) && durEl.TryGetDouble(out var durVal) ? durVal : 0;

                string? previewUrl = null;
                if (el.TryGetProperty("previews", out var prevEl) && prevEl.ValueKind == JsonValueKind.Object)
                {
                    if (prevEl.TryGetProperty("preview-hq-mp3", out var hq) && hq.ValueKind == JsonValueKind.String) previewUrl = hq.GetString();
                    else if (prevEl.TryGetProperty("preview-lq-mp3", out var lq) && lq.ValueKind == JsonValueKind.String) previewUrl = lq.GetString();
                }

                if (id > 0 && !string.IsNullOrWhiteSpace(previewUrl))
                {
                    items.Add(new FreesoundSearchItem { Id = id, Name = name, Duration = duration, PreviewUrl = previewUrl });
                }
            }

            var count = doc.RootElement.TryGetProperty("count", out var countEl) && countEl.TryGetInt32(out var countVal) ? countVal : items.Count;
            return Ok(new { count, results = items });
        }
    }
}
