using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Subscriptions.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Subscriptions.Commands.UpdateSubscriptionPlan;

public class UpdateSubscriptionPlanCommandHandler : IRequestHandler<UpdateSubscriptionPlanCommand, Result<SubscriptionPlanDto>>
{
    private readonly IApplicationDbContext _context;

    public UpdateSubscriptionPlanCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<SubscriptionPlanDto>> Handle(UpdateSubscriptionPlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await _context.SubscriptionPlans
            .Include(x => x.Limits)
            .FirstOrDefaultAsync(x => x.SubscriptionPlanId == request.SubscriptionPlanId, cancellationToken);
        if (plan == null)
        {
            return Result<SubscriptionPlanDto>.Failure("SUBSCRIPTION_PLAN_NOT_FOUND", "Subscription plan not found.");
        }

        var duplicateType = await _context.SubscriptionPlans
            .AsNoTracking()
            .AnyAsync(x => x.SubscriptionPlanId != request.SubscriptionPlanId && x.PlanType == request.PlanType, cancellationToken);

        if (duplicateType)
        {
            return Result<SubscriptionPlanDto>.Failure("SUBSCRIPTION_PLAN_EXISTS", "Subscription plan type already exists.");
        }

        plan.PlanType = request.PlanType;
        plan.Name = request.Name.Trim();
        plan.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        plan.PriceVnd = request.PriceVnd;
        plan.DurationDays = request.DurationDays;
        plan.IsActive = request.IsActive;
        plan.DisplayOrder = request.DisplayOrder;

        if (request.Limits != null)
        {
            var incomingLimits = BuildNormalizedLimits(request.Limits);
            var existingByFeature = plan.Limits.ToDictionary(x => x.FeatureKey);
            var incomingFeatures = incomingLimits.Select(x => x.FeatureKey).ToHashSet();

            var toRemove = plan.Limits.Where(x => !incomingFeatures.Contains(x.FeatureKey)).ToList();
            if (toRemove.Count > 0)
            {
                _context.SubscriptionPlanLimits.RemoveRange(toRemove);
            }

            foreach (var incoming in incomingLimits)
            {
                if (existingByFeature.TryGetValue(incoming.FeatureKey, out var existing))
                {
                    existing.LimitCount = incoming.LimitCount;
                    existing.WindowType = incoming.WindowType;
                    existing.IsEnabled = incoming.IsEnabled;
                }
                else
                {
                    plan.Limits.Add(new SubscriptionPlanLimit
                    {
                        SubscriptionPlanLimitId = NewId.NextGuid(),
                        SubscriptionPlanId = plan.SubscriptionPlanId,
                        FeatureKey = incoming.FeatureKey,
                        LimitCount = incoming.LimitCount,
                        WindowType = incoming.WindowType,
                        IsEnabled = incoming.IsEnabled
                    });
                }
            }
        }

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

    private static List<SubscriptionPlanLimitInputDto> BuildNormalizedLimits(List<SubscriptionPlanLimitInputDto> inputLimits)
    {
        return inputLimits
            .GroupBy(x => x.FeatureKey)
            .Select(g => g.Last())
            .ToList();
    }
}
