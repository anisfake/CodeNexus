using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.Payments.DTOs;

public record VnPayCreatePaymentResponseDto(
    Guid PaymentTransactionId,
    string TxnRef,
    string PaymentUrl
);

public record VnPayCallbackResponseDto(
    Guid PaymentTransactionId,
    PaymentStatus Status,
    string ResponseCode,
    decimal Amount,
    Guid? SubscriptionPlanId,
    DateTime? PlanExpiresAt
);
