namespace Management.Domain.Entities;

public class PaymentTransaction
{
    public Guid TransactionId { get; set; }
    public Guid UserId { get; set; }
    public string Type { get; set; } = string.Empty; // topup | withdraw
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty; // pending | success | failed
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
