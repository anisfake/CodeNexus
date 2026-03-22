using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Infrastructure.Services;

public class SubscriptionAccessService : ISubscriptionAccessService
{
    private readonly IApplicationDbContext _context;

    public SubscriptionAccessService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SubscriptionPlan> GetEffectivePlanAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var userPlan = await _context.Users
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new { x.SubscriptionPlanId, x.PlanExpiresAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (userPlan?.SubscriptionPlanId != null
            && userPlan.PlanExpiresAt.HasValue
            && userPlan.PlanExpiresAt.Value > DateTime.UtcNow)
        {
            var activePlan = await _context.SubscriptionPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.SubscriptionPlanId == userPlan.SubscriptionPlanId.Value && x.IsActive, cancellationToken);

            if (activePlan != null)
            {
                return activePlan;
            }
        }

        var freePlan = await _context.SubscriptionPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PlanType == SubscriptionPlanType.Free, cancellationToken);

        if (freePlan != null)
        {
            return freePlan;
        }

        // Safety fallback when DB seed is missing to avoid runtime crash.
        return new SubscriptionPlan
        {
            SubscriptionPlanId = Guid.Empty,
            PlanType = SubscriptionPlanType.Free,
            Name = "Free",
            IsActive = true,
            DurationDays = 0,
            PriceVnd = 0m
        };
    }

    public async Task<bool> CanUsePersonalGoalsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var plan = await GetEffectivePlanAsync(userId, cancellationToken);
        return plan.PlanType != SubscriptionPlanType.Free;
    }

    public async Task<bool> CanUsePaidModelsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var plan = await GetEffectivePlanAsync(userId, cancellationToken);
        return plan.PlanType != SubscriptionPlanType.Free;
    }
}
