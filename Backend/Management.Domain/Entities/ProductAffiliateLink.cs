using System;

namespace Management.Domain.Entities
{
    public class ProductAffiliateLink
    {
        public long Id { get; set; }
        public string ExternalItemId { get; set; } = string.Empty;
        public string ProductLink { get; set; } = string.Empty;
        public string? SubId1 { get; set; }
        public string? SubId2 { get; set; }
        public string? SubId3 { get; set; }
        public string? SubId4 { get; set; }
        public string? SubId5 { get; set; }
        public string? AffiliateLink { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
