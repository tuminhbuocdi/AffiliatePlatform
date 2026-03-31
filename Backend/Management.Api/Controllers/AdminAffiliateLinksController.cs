using Management.Domain.Entities;
using Management.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Management.Api.Controllers
{
    [ApiController]
    [Route("api/admin/affiliate-links")]
    [Authorize(Roles = "admin")]
    public class AdminAffiliateLinksController : ControllerBase
    {
        private readonly ProductAffiliateLinkRepository _affiliateLinks;

        public AdminAffiliateLinksController(ProductAffiliateLinkRepository affiliateLinks)
        {
            _affiliateLinks = affiliateLinks;
        }

        [HttpGet]
        public async Task<IActionResult> GetList([FromQuery] string? search, [FromQuery] string? filter = null, [FromQuery] int limit = 200)
        {
            var take = Math.Max(1, Math.Min(limit, 500));
            var rows = await _affiliateLinks.GetForExportAsync(search, filter);
            return Ok(rows.Take(take));
        }

        [HttpGet("{externalItemId}")]
        public async Task<IActionResult> GetByExternalItemId([FromRoute] string externalItemId)
        {
            var item = await _affiliateLinks.GetByExternalItemIdAsync(externalItemId);
            if (item == null) return NotFound();

            return Ok(item);
        }

        public class UpsertAffiliateLinkRequest
        {
            public string ExternalItemId { get; set; } = string.Empty;
            public string ProductLink { get; set; } = string.Empty;
            public string? SubId1 { get; set; }
            public string? SubId2 { get; set; }
            public string? SubId3 { get; set; }
            public string? SubId4 { get; set; }
            public string? SubId5 { get; set; }
            public string? AffiliateLink { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] UpsertAffiliateLinkRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.ExternalItemId)) return BadRequest("ExternalItemId is required.");
            if (string.IsNullOrWhiteSpace(req.ProductLink)) return BadRequest("ProductLink is required.");

            var existing = await _affiliateLinks.GetByExternalItemIdAsync(req.ExternalItemId.Trim());
            if (existing != null) return Conflict("ExternalItemId already exists.");

            var now = DateTime.UtcNow;
            var model = new ProductAffiliateLink
            {
                ExternalItemId = req.ExternalItemId.Trim(),
                ProductLink = req.ProductLink.Trim(),
                SubId1 = string.IsNullOrWhiteSpace(req.SubId1) ? null : req.SubId1.Trim(),
                SubId2 = string.IsNullOrWhiteSpace(req.SubId2) ? null : req.SubId2.Trim(),
                SubId3 = string.IsNullOrWhiteSpace(req.SubId3) ? null : req.SubId3.Trim(),
                SubId4 = string.IsNullOrWhiteSpace(req.SubId4) ? null : req.SubId4.Trim(),
                SubId5 = string.IsNullOrWhiteSpace(req.SubId5) ? null : req.SubId5.Trim(),
                AffiliateLink = string.IsNullOrWhiteSpace(req.AffiliateLink) ? null : req.AffiliateLink.Trim(),
                CreatedAt = now,
                UpdatedAt = now,
            };

            await _affiliateLinks.UpsertAsync(model);
            return Ok(new { success = true });
        }

        [HttpPut("{externalItemId}")]
        public async Task<IActionResult> Update([FromRoute] string externalItemId, [FromBody] UpsertAffiliateLinkRequest req)
        {
            if (string.IsNullOrWhiteSpace(externalItemId)) return BadRequest("ExternalItemId is required.");

            var existing = await _affiliateLinks.GetByExternalItemIdAsync(externalItemId.Trim());
            if (existing == null) return NotFound();

            var model = new ProductAffiliateLink
            {
                ExternalItemId = existing.ExternalItemId,
                ProductLink = string.IsNullOrWhiteSpace(req.ProductLink) ? existing.ProductLink : req.ProductLink.Trim(),
                SubId1 = req.SubId1 == null ? existing.SubId1 : (string.IsNullOrWhiteSpace(req.SubId1) ? null : req.SubId1.Trim()),
                SubId2 = req.SubId2 == null ? existing.SubId2 : (string.IsNullOrWhiteSpace(req.SubId2) ? null : req.SubId2.Trim()),
                SubId3 = req.SubId3 == null ? existing.SubId3 : (string.IsNullOrWhiteSpace(req.SubId3) ? null : req.SubId3.Trim()),
                SubId4 = req.SubId4 == null ? existing.SubId4 : (string.IsNullOrWhiteSpace(req.SubId4) ? null : req.SubId4.Trim()),
                SubId5 = req.SubId5 == null ? existing.SubId5 : (string.IsNullOrWhiteSpace(req.SubId5) ? null : req.SubId5.Trim()),
                AffiliateLink = req.AffiliateLink == null ? existing.AffiliateLink : (string.IsNullOrWhiteSpace(req.AffiliateLink) ? null : req.AffiliateLink.Trim()),
                CreatedAt = existing.CreatedAt,
                UpdatedAt = DateTime.UtcNow,
            };

            await _affiliateLinks.UpsertAsync(model);
            return Ok(new { success = true });
        }

        [HttpDelete("{externalItemId}")]
        public async Task<IActionResult> Delete([FromRoute] string externalItemId)
        {
            var existing = await _affiliateLinks.GetByExternalItemIdAsync(externalItemId);
            if (existing == null) return NotFound();

            await _affiliateLinks.DeleteByExternalItemIdAsync(externalItemId);
            return Ok(new { success = true });
        }
    }
}
