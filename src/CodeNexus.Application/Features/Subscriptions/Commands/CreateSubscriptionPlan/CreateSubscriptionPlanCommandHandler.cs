using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Subscriptions.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Subscriptions.Commands.CreateSubscriptionPlan;

public class CreateSubscriptionPlanCommandHandler : IRequestHandler<CreateSubscriptionPlanCommand, Result<SubscriptionPlanDto>>
{
    private readonly IApplicationDbContext _context;

    public CreateSubscriptionPlanCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<SubscriptionPlanDto>> Handle(CreateSubscriptionPlanCommand request, CancellationToken cancellationToken)
    {
        var exists = await _context.SubscriptionPlans
            .AsNoTracking()
            .AnyAsync(x => x.PlanType == request.PlanType, cancellationToken);

        if (exists)
        {
            return Result<SubscriptionPlanDto>.Failure("SUBSCRIPTION_PLAN_EXISTS", "Subscription plan type already exists.");
        }

        var plan = new SubscriptionPlan
        {
            SubscriptionPlanId = NewId.NextGuid(),
            PlanType = request.PlanType,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            PriceVnd = request.PriceVnd,
            DurationDays = request.DurationDays,
            IsActive = request.IsActive,
            DisplayOrder = request.DisplayOrder
        };

        foreach (var limit in BuildNormalizedLimits(request.Limits))
        {
            plan.Limits.Add(limit);
        }

        await _context.SubscriptionPlans.AddAsync(plan, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<SubscriptionPlanDto>.Success(new SubscriptionPlanDto(
            plan.SubscriptionPlanId,
            plan.PlanType,
            plan.Name,
            plan.Description,
            plan.PriceVnd,
            plan.DurationDays,
            plan.IsActive,
            plan.DisplayOrder,
            plan.Limits.Select(x => new SubscriptionPlanLimitDto(
                x.FeatureKey,
                x.LimitCount,
                x.WindowType,
                x.IsEnabled)).ToList()));
    }

    private static List<SubscriptionPlanLimit> BuildNormalizedLimits(List<SubscriptionPlanLimitInputDto>? inputLimits)
    {
        var defaultLimits = new[]
        {
            new SubscriptionPlanLimitInputDto(SubscriptionFeatureKey.LearningPathCreation, null, UsageWindowType.Monthly, true),
            new SubscriptionPlanLimitInputDto(SubscriptionFeatureKey.TutorMessages, null, UsageWindowType.Monthly, true),
            new SubscriptionPlanLimitInputDto(SubscriptionFeatureKey.FocusSessionReview, null, UsageWindowType.Monthly, true)
        };

        var selected = inputLimits?.Count > 0 ? inputLimits : defaultLimits.ToList();

        return selected
            .GroupBy(x => x.FeatureKey)
            .Select(g => g.Last())
            .Select(x => new SubscriptionPlanLimit
            {
                SubscriptionPlanLimitId = NewId.NextGuid(),
                FeatureKey = x.FeatureKey,
                LimitCount = x.LimitCount,
                WindowType = x.WindowType,
                IsEnabled = x.IsEnabled
            })
            .ToList();
    }
}
