using Management.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Management.Api.Controllers;

[ApiController]
[Route("api/admin/generated-subtitles")]
[Authorize(Roles = "admin")]
public sealed class AdminGeneratedSubtitlesController : ControllerBase
{
    private readonly GeneratedSubtitleRepository _repo;

    public AdminGeneratedSubtitlesController(GeneratedSubtitleRepository repo)
    {
        _repo = repo;
    }

    public sealed class GeneratedSubtitleDto
    {
        public Guid Id { get; set; }
        public string SourceFileName { get; set; } = string.Empty;
        public Guid? AssTemplateId { get; set; }
        public string AssText { get; set; } = string.Empty;
        public string CreatedAtUtc { get; set; } = string.Empty;
        public string UpdatedAtUtc { get; set; } = string.Empty;
    }

    private static GeneratedSubtitleDto Map(GeneratedSubtitleRepository.GeneratedSubtitleRow x) => new()
    {
        Id = x.Id,
        SourceFileName = x.SourceFileName,
        AssTemplateId = x.AssTemplateId,
        AssText = x.AssText,
        CreatedAtUtc = x.CreatedAtUtc.ToString("o"),
        UpdatedAtUtc = x.UpdatedAtUtc.ToString("o"),
    };

    public sealed class CreateRequest
    {
        public string SourceFileName { get; set; } = string.Empty;
        public Guid? AssTemplateId { get; set; }
        public string AssText { get; set; } = string.Empty;
    }

    public sealed class UpdateNameRequest
    {
        public string SourceFileName { get; set; } = string.Empty;
    }

    [HttpGet]
    public async Task<IActionResult> GetLatest([FromQuery] int limit = 50)
    {
        var items = await _repo.GetLatestAsync(limit);
        return Ok(items.Select(Map).ToArray());
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById([FromRoute] Guid id)
    {
        var item = await _repo.GetByIdAsync(id);
        if (item == null) return NotFound();
        return Ok(Map(item));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRequest req)
    {
        if (req == null) return BadRequest("Invalid request");
        req.SourceFileName = (req.SourceFileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(req.SourceFileName)) return BadRequest("SourceFileName is required");
        req.AssText = (req.AssText ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(req.AssText)) return BadRequest("AssText is required");

        var id = await _repo.CreateAsync(req.SourceFileName, req.AssText, req.AssTemplateId);
        var created = await _repo.GetByIdAsync(id);
        if (created == null) return StatusCode(500, "Failed to load created subtitle");
        return Ok(Map(created));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete([FromRoute] Guid id)
    {
        var ok = await _repo.DeleteAsync(id);
        if (!ok) return NotFound();
        return Ok(new { ok = true });
    }

    [HttpPut("{id:guid}/name")]
    public async Task<IActionResult> UpdateName([FromRoute] Guid id, [FromBody] UpdateNameRequest req)
    {
        if (req == null) return BadRequest("Invalid request");
        req.SourceFileName = (req.SourceFileName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(req.SourceFileName)) return BadRequest("SourceFileName is required");

        var ok = await _repo.UpdateSourceFileNameAsync(id, req.SourceFileName);
        if (!ok) return NotFound();

        var updated = await _repo.GetByIdAsync(id);
        if (updated == null) return NotFound();
        return Ok(Map(updated));
    }
}
