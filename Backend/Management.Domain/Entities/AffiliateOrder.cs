using System;

namespace Management.Domain.Entities
{
    public class AffiliateOrder
    {
        public long Id { get; set; }
        public long ProductId { get; set; }
        public DateTime OrderTime { get; set; }
        public decimal OrderAmount { get; set; }
        public decimal CommissionAmount { get; set; }
        public string Status { get; set; } = string.Empty; // Pending / Approved / Rejected
        public Guid? MatchedUserId { get; set; }
        public DateTime CreatedAt { get; set; }
        
        // Navigation properties
        public Product Product { get; set; } = null!;
        public User? MatchedUser { get; set; }
        public ICollection<PointHistory> PointHistories { get; set; } = new List<PointHistory>();
    }
}
