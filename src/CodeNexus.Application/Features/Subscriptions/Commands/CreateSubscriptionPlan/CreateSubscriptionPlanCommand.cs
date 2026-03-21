using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Subscriptions.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;

namespace CodeNexus.Application.Features.Subscriptions.Commands.CreateSubscriptionPlan;

public record CreateSubscriptionPlanCommand(
    SubscriptionPlanType PlanType,
    string Name,
    string? Description,
    decimal PriceVnd,
    int DurationDays,
    bool IsActive,
    int DisplayOrder) : IRequest<Result<SubscriptionPlanDto>>;
