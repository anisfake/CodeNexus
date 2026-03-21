using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Subscriptions.DTOs;
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
        var plan = await _context.SubscriptionPlans.FirstOrDefaultAsync(x => x.SubscriptionPlanId == request.SubscriptionPlanId, cancellationToken);
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

        await _context.SaveChangesAsync(cancellationToken);

        return Result<SubscriptionPlanDto>.Success(new SubscriptionPlanDto(
            plan.SubscriptionPlanId,
            plan.PlanType,
            plan.Name,
            plan.Description,
            plan.PriceVnd,
            plan.DurationDays,
            plan.IsActive,
            plan.DisplayOrder));
    }
}
