using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.Subscriptions.DTOs;

public record SubscriptionPlanDto(
    Guid SubscriptionPlanId,
    SubscriptionPlanType PlanType,
    string Name,
    string? Description,
    decimal PriceVnd,
    int DurationDays,
    bool IsActive,
    int DisplayOrder
);

public record CurrentSubscriptionDto(
    Guid SubscriptionPlanId,
    SubscriptionPlanType PlanType,
    string Name,
    DateTime? ExpiresAt,
    bool IsFreeFallback
);
