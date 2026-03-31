using System;

namespace Management.Domain.Entities
{
    public class AffiliateClick
    {
        public long Id { get; set; }
        public Guid UserId { get; set; }
        public long ProductId { get; set; }
        public DateTime ClickTime { get; set; }
        public string? IPAddress { get; set; }
        public string? UserAgent { get; set; }
        public bool IsProcessed { get; set; }
        
        // Navigation properties
        public User User { get; set; } = null!;
        public Product Product { get; set; } = null!;
    }
}
