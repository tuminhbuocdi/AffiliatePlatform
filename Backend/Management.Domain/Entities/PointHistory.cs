using System;

namespace Management.Domain.Entities
{
    public class PointHistory
    {
        public long Id { get; set; }
        public Guid UserId { get; set; }
        public long? OrderId { get; set; }
        public int Points { get; set; }
        public string Type { get; set; } = string.Empty; // Earn / Redeem / Adjust
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        
        // Navigation properties
        public User User { get; set; } = null!;
        public AffiliateOrder? Order { get; set; }
    }
}
