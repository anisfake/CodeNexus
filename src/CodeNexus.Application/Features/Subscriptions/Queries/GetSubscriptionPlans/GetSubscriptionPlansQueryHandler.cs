using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Subscriptions.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Subscriptions.Queries.GetSubscriptionPlans;

public class GetSubscriptionPlansQueryHandler : IRequestHandler<GetSubscriptionPlansQuery, List<SubscriptionPlanDto>>
{
    private readonly IApplicationDbContext _context;

    public GetSubscriptionPlansQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<SubscriptionPlanDto>> Handle(GetSubscriptionPlansQuery request, CancellationToken cancellationToken)
    {
        var query = _context.SubscriptionPlans.AsNoTracking();
        if (request.ActiveOnly)
        {
            query = query.Where(x => x.IsActive);
        }

        var plans = await query
            .Include(x => x.Limits)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.PriceVnd)
            .ToListAsync(cancellationToken);

        return plans
            .Select(x => new SubscriptionPlanDto(
                x.SubscriptionPlanId,
                x.PlanType,
                x.Name,
                x.Description,
                x.PriceVnd,
                x.DurationDays,
                x.IsActive,
                x.DisplayOrder,
                x.Limits
                    .OrderBy(l => l.FeatureKey)
                    .Select(l => new SubscriptionPlanLimitDto(
                        l.FeatureKey,
                        l.LimitCount,
                        l.WindowType,
                        l.IsEnabled))
                    .ToList()))
            .ToList();
    }
}
