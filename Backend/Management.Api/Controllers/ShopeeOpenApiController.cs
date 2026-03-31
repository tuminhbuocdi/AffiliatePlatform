using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Management.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ShopeeOpenApiController : ControllerBase
    {
        [HttpGet("search")]
        [Authorize]
        public async Task<IActionResult> SearchItems(
            [FromQuery] string keyword,
            [FromQuery] int page = 1,
            [FromQuery] int page_size = 20,
            [FromQuery] long? category_id = null,
            [FromQuery] string? sort = null,
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            return StatusCode(410, "Shopee OpenAPI is no longer supported");
        }

        [HttpGet("detail/{itemid:long}")]
        [Authorize]
        public async Task<IActionResult> GetItemDetail(long itemid, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            return StatusCode(410, "Shopee OpenAPI is no longer supported");
        }

        [HttpGet("link/{itemid:long}")]
        [Authorize]
        public async Task<IActionResult> GenerateAffiliateLink(long itemid, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            return StatusCode(410, "Shopee OpenAPI is no longer supported");
        }
    }
}
