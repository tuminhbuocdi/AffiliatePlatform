using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Management.Infrastructure.External;

namespace Management.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccessTradeController : ControllerBase
    {
        private readonly IAccessTradeApiService _accessTradeApiService;

        public AccessTradeController(IAccessTradeApiService accessTradeApiService)
        {
            _accessTradeApiService = accessTradeApiService;
        }

        // This endpoint is for internal/backoffice usage.
        // Keep it authorized because it can expose sensitive affiliate data.
        [HttpGet("orders")]
        [Authorize]
        public async Task<IActionResult> GetOrders(
            [FromQuery] DateTime since,
            [FromQuery] DateTime until,
            [FromQuery] int page = 1,
            [FromQuery(Name = "utm_source")] string? utmSource = null,
            [FromQuery(Name = "utm_campaign")] string? utmCampaign = null,
            [FromQuery(Name = "utm_medium")] string? utmMedium = null,
            [FromQuery(Name = "utm_content")] string? utmContent = null,
            [FromQuery] int? limit = null,
            [FromQuery] int? status = null,
            [FromQuery] string? merchant = null,
            CancellationToken cancellationToken = default)
        {
            var json = await _accessTradeApiService.GetOrderListAsync(
                since,
                until,
                page,
                utmSource,
                utmCampaign,
                utmMedium,
                utmContent,
                limit,
                status,
                merchant,
                cancellationToken);
            return Content(json.RootElement.GetRawText(), "application/json");
        }

        [HttpGet("datafeeds")]
        [Authorize]
        public async Task<IActionResult> GetDataFeeds(
            [FromQuery] string domain,
            [FromQuery] string? cat = null,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 50,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(domain))
            {
                return BadRequest("domain is required");
            }

            var products = await _accessTradeApiService.GetDataFeedsAsync(domain, cat, page, limit, cancellationToken);
            return Ok(products);
        }
    }
}
