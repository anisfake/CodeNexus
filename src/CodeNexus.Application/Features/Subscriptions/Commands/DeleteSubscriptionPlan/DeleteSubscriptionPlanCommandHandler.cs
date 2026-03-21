using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Subscriptions.Commands.DeleteSubscriptionPlan;

public class DeleteSubscriptionPlanCommandHandler : IRequestHandler<DeleteSubscriptionPlanCommand, Result<string>>
{
    private readonly IApplicationDbContext _context;

    public DeleteSubscriptionPlanCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<string>> Handle(DeleteSubscriptionPlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await _context.SubscriptionPlans.FirstOrDefaultAsync(x => x.SubscriptionPlanId == request.SubscriptionPlanId, cancellationToken);
        if (plan == null)
        {
            return Result<string>.Failure("SUBSCRIPTION_PLAN_NOT_FOUND", "Subscription plan not found.");
        }

        if (plan.PlanType == SubscriptionPlanType.Free)
        {
            return Result<string>.Failure("CANNOT_DELETE_FREE_PLAN", "Free plan cannot be deleted.");
        }

        _context.SubscriptionPlans.Remove(plan);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<string>.Success("Subscription plan deleted successfully.");
    }
}
