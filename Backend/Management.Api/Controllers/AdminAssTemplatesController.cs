using Management.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Management.Api.Controllers;

[ApiController]
[Route("api/admin/ass-templates")]
[Authorize(Roles = "admin")]
public sealed class AdminAssTemplatesController : ControllerBase
{
    private readonly AssTemplateRepository _repo;

    public AdminAssTemplatesController(AssTemplateRepository repo)
    {
        _repo = repo;
    }

    public sealed class TemplateDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Tags { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string CreatedAtUtc { get; set; } = string.Empty;
        public string UpdatedAtUtc { get; set; } = string.Empty;
    }

    private static TemplateDto Map(AssTemplateRepository.AssTemplateRow x) => new()
    {
        Id = x.Id,
        Name = x.Name,
        Tags = x.Tags,
        Content = x.Content,
        IsActive = x.IsActive,
        CreatedAtUtc = x.CreatedAtUtc.ToString("o"),
        UpdatedAtUtc = x.UpdatedAtUtc.ToString("o"),
    };

    public sealed class UpsertRequest
    {
        public Guid? Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Tags { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool? isActive = null, [FromQuery] string? q = null)
    {
        var items = await _repo.GetAllAsync(isActive, q);
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
    public async Task<IActionResult> Upsert([FromBody] UpsertRequest req)
    {
        if (req == null) return BadRequest("Invalid request");
        req.Name = (req.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest("Name is required");
        req.Content = (req.Content ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(req.Content)) return BadRequest("Content is required");

        if (req.Id == null || req.Id == Guid.Empty)
        {
            var id = await _repo.CreateAsync(req.Name, req.Tags, req.Content, req.IsActive);
            var created = await _repo.GetByIdAsync(id);
            if (created == null) return StatusCode(500, "Failed to load created template");
            return Ok(Map(created));
        }

        var ok = await _repo.UpdateAsync(req.Id.Value, req.Name, req.Tags, req.Content, req.IsActive);
        if (!ok) return NotFound();

        var updated = await _repo.GetByIdAsync(req.Id.Value);
        if (updated == null) return StatusCode(500, "Failed to load updated template");
        return Ok(Map(updated));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete([FromRoute] Guid id)
    {
        var ok = await _repo.DeleteAsync(id);
        if (!ok) return NotFound();
        return Ok(new { ok = true });
    }
}
