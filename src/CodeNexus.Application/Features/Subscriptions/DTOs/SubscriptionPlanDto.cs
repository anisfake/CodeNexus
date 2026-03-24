using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.Subscriptions.DTOs;

public record SubscriptionPlanLimitDto(
    SubscriptionFeatureKey FeatureKey,
    int? LimitCount,
    UsageWindowType WindowType,
    bool IsEnabled
);

public record SubscriptionPlanLimitInputDto(
    SubscriptionFeatureKey FeatureKey,
    int? LimitCount,
    UsageWindowType WindowType,
    bool IsEnabled
);

public record SubscriptionPlanDto(
    Guid SubscriptionPlanId,
    SubscriptionPlanType PlanType,
    string Name,
    string? Description,
    decimal PriceVnd,
    int DurationDays,
    bool IsActive,
    int DisplayOrder,
    List<SubscriptionPlanLimitDto> Limits
);

public record CurrentSubscriptionDto(
    Guid SubscriptionPlanId,
    SubscriptionPlanType PlanType,
    string Name,
    DateTime? ExpiresAt,
    bool IsFreeFallback,
    List<SubscriptionPlanLimitDto> Limits
);

