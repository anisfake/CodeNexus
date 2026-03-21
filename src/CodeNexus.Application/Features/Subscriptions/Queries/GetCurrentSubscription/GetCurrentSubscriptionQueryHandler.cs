using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Subscriptions.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Subscriptions.Queries.GetCurrentSubscription;

public class GetCurrentSubscriptionQueryHandler : IRequestHandler<GetCurrentSubscriptionQuery, CurrentSubscriptionDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISubscriptionAccessService _subscriptionAccessService;

    public GetCurrentSubscriptionQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ISubscriptionAccessService subscriptionAccessService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _subscriptionAccessService = subscriptionAccessService;
    }

    public async Task<CurrentSubscriptionDto> Handle(GetCurrentSubscriptionQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();
        var plan = await _subscriptionAccessService.GetEffectivePlanAsync(userId, cancellationToken);
        var expiresAt = await _context.Users
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.PlanExpiresAt)
            .FirstOrDefaultAsync(cancellationToken);
        var isFreePlan = string.Equals(
            plan.PlanType.ToString(),
            SubscriptionPlanType.Free.ToString(),
            StringComparison.OrdinalIgnoreCase);

        return new CurrentSubscriptionDto(
            plan.SubscriptionPlanId,
            plan.PlanType,
            plan.Name,
            isFreePlan ? null : expiresAt,
            isFreePlan);
    }
}
