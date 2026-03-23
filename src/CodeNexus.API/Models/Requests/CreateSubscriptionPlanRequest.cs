using CodeNexus.Domain.Enums;
using CodeNexus.Application.Features.Subscriptions.DTOs;

namespace CodeNexus.API.Models.Requests;

public record CreateSubscriptionPlanRequest(
    SubscriptionPlanType PlanType,
    string Name,
    string? Description,
    decimal PriceVnd,
    int DurationDays,
    bool IsActive,
    int DisplayOrder,
    List<SubscriptionPlanLimitInputDto>? Limits
);
