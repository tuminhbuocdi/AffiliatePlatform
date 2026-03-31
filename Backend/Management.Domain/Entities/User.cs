using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace Management.Domain.Entities;

public class User
{
    public Guid UserId { get; set; }
    public string Username { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string PasswordHash { get; set; }
    public string? FullName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? TransferNoteCode { get; set; }
    public bool IsActive { get; set; }
    public bool IsLocked { get; set; }
    public bool IsDeleted { get; set; }
    public string? UserRole { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public string? LastLoginIp { get; set; }
    public bool EmailVerified { get; set; }
    public bool PhoneVerified { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEnd { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    
    // Affiliate fields
    public int TotalPoints { get; set; }
    public int Level { get; set; }
    
    // Navigation properties
    public ICollection<AffiliateClick> AffiliateClicks { get; set; } = new List<AffiliateClick>();
    public ICollection<AffiliateOrder> MatchedOrders { get; set; } = new List<AffiliateOrder>();
    public ICollection<PointHistory> PointHistories { get; set; } = new List<PointHistory>();
    public ICollection<RewardRedemption> RewardRedemptions { get; set; } = new List<RewardRedemption>();
}
