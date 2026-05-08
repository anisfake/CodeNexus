using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.Payments.DTOs;

public record BillingTransactionResponse(
    Guid PaymentTransactionId,
    Guid UserId,
    string? Username,
    string? Email,
    decimal Amount,
    string Provider,
    string TxnRef,
    PaymentStatus Status,
    string? ResponseCode,
    string? TransactionNo,
    string? BankCode,
    string? OrderInfo,
    DateTime? PaidAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record MyBillingTransactionResponse(
    Guid PaymentTransactionId,
    Guid? TokenPackageId,
    string? TokenPackageName,
    decimal Amount,
    decimal CreditedTokens,
    string Provider,
    string TxnRef,
    string OrderInfo,
    PaymentStatus Status,
    string? ResponseCode,
    string? TransactionNo,
    string? BankCode,
    DateTime? PaidAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record MyBillingTransactionDetailResponse(
    Guid PaymentTransactionId,
    Guid? TokenPackageId,
    string? TokenPackageName,
    decimal Amount,
    decimal CreditedTokens,
    string Provider,
    string TxnRef,
    string OrderInfo,
    PaymentStatus Status,
    string? ResponseCode,
    string? TransactionNo,
    string? BankCode,
    DateTime? PaidAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record BillingTransactionDetailResponse(
    Guid PaymentTransactionId,
    Guid UserId,
    string? Username,
    string? Email,
    decimal Amount,
    string Provider,
    string TxnRef,
    string OrderInfo,
    PaymentStatus Status,
    string? ResponseCode,
    string? TransactionNo,
    string? BankCode,
    DateTime? PaidAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record BillingSummaryItemResponse(
    DateOnly Date,
    int Transactions,
    int SuccessfulTransactions,
    decimal RevenueVnd
);

public record BillingSummaryResponse(
    DateTime? FromUtc,
    DateTime? ToUtc,
    int TotalTransactions,
    int PendingTransactions,
    int SuccessfulTransactions,
    int FailedTransactions,
    int CanceledTransactions,
    decimal TotalRevenueVnd,
    List<BillingSummaryItemResponse> DailyRevenue
);

public record MonthlyFinanceOverviewResponse(
    decimal PackageProfitVnd,
    decimal TotalAiCostUsd
);

