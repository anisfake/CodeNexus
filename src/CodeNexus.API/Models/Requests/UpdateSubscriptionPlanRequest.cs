using CodeNexus.Domain.Enums;

namespace CodeNexus.API.Models.Requests;

public record UpdateSubscriptionPlanRequest(
    SubscriptionPlanType PlanType,
    string Name,
    string? Description,
    decimal PriceVnd,
    int DurationDays,
    bool IsActive,
    int DisplayOrder
);
