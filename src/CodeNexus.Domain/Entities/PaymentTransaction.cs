using CodeNexus.Domain.Enums;

namespace CodeNexus.Domain.Entities;

public class PaymentTransaction
{
    public Guid PaymentTransactionId { get; set; }
    public Guid UserId { get; set; }
    public virtual User User { get; set; } = null!;
    public Guid? SubscriptionPlanId { get; set; }
    public Guid? TokenPackageId { get; set; }
    public virtual TokenPackage? TokenPackage { get; set; }

    public decimal Amount { get; set; }
    public decimal CreditedTokens { get; set; }

    public string Provider { get; set; } = "VNPAY";
    public string TxnRef { get; set; } = string.Empty;
    public string OrderInfo { get; set; } = string.Empty;

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? ResponseCode { get; set; }
    public string? TransactionNo { get; set; }
    public string? BankCode { get; set; }
    public DateTime? PaidAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
