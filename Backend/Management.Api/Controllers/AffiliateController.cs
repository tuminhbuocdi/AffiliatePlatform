using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Management.Infrastructure.Repositories;

namespace Management.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AffiliateController : ControllerBase
    {
        private readonly IAffiliateClickRepository _affiliateClickRepository;
        private readonly IProductRepository _productRepository;

        public AffiliateController(
            IAffiliateClickRepository affiliateClickRepository,
            IProductRepository productRepository)
        {
            _affiliateClickRepository = affiliateClickRepository;
            _productRepository = productRepository;
        }

        [HttpGet("clicks")]
        public async Task<IActionResult> GetMyClicks()
        {
            var userId = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var clicks = await _affiliateClickRepository.GetByUserIdAsync(Guid.Parse(userId));
            return Ok(clicks);
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var userId = User.FindFirst("UserId")?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            var stats = await _affiliateClickRepository.GetStatsByUserIdAsync(Guid.Parse(userId), DateTime.UtcNow.AddDays(-7));

            return Ok(stats);
        }
    }

    public class AffiliateStats
    {
        public int TotalClicks { get; set; }
        public int UniqueProducts { get; set; }
        public int RecentClicks { get; set; }
    }
}
