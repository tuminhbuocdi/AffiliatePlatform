using System;

namespace Management.Domain.Entities
{
    public class RewardRedemption
    {
        public long Id { get; set; }
        public Guid UserId { get; set; }
        public int RewardId { get; set; }
        public string Status { get; set; } = string.Empty; // Pending / Completed / Cancelled
        public DateTime CreatedAt { get; set; }
        
        // Navigation properties
        public User User { get; set; } = null!;
        public Reward Reward { get; set; } = null!;
    }
}
