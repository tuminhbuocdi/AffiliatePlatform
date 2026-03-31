using Management.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Management.Api.Controllers
{
    [ApiController]
    [Route("api/admin/affiliate-picker")]
    [Authorize(Roles = "admin")]
    public class AdminAffiliatePickerController : ControllerBase
    {
        private readonly IProductRepository _products;
        private readonly IConfiguration _config;

        public AdminAffiliatePickerController(IProductRepository products, IConfiguration config)
        {
            _products = products;
            _config = config;
        }

        private string GetShopeeCdnImagePrefix()
        {
            var prefix = _config["Shopee:CdnImagePrefix"] ?? string.Empty;
            return prefix.Trim();
        }

        private static string? BuildCdnUrl(string prefix, string? key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            if (Uri.TryCreate(key, UriKind.Absolute, out _)) return key;

            var p = (prefix ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(p)) return key;
            if (!p.EndsWith("/")) p += "/";
            return p + key.TrimStart('/');
        }

        [HttpGet("missing-social-links")]
        public async Task<IActionResult> GetMissingSocialLinks([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var (items, total) = await _products.GetAffiliateLinkPickerPageAsync(search, page, pageSize);
            var imagePrefix = GetShopeeCdnImagePrefix();

            var result = items.Select(x => new
            {
                externalItemId = x.ExternalItemId,
                name = x.Name,
                imageUrl = BuildCdnUrl(imagePrefix, x.ImageKey),
                affiliateLink = x.AffiliateLink,
                hasSocialLinks = x.HasSocialLinks,
            });

            return Ok(new
            {
                items = result,
                total,
                page = Math.Max(1, page),
                pageSize = Math.Max(1, Math.Min(pageSize, 50)),
            });
        }
    }
}
