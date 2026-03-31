using System;

namespace Management.Domain.Entities
{
    public class ProductSocialLink
    {
        public long Id { get; set; }
        public string ProductLink { get; set; } = string.Empty;
        public string AffiliateLink { get; set; } = string.Empty;
        public string SocialLink { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
