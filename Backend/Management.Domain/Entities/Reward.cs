using System;

namespace Management.Domain.Entities
{
    public class Reward
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int PointsRequired { get; set; }
        public int Stock { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        
        // Navigation properties
        public ICollection<RewardRedemption> RewardRedemptions { get; set; } = new List<RewardRedemption>();
    }
}
