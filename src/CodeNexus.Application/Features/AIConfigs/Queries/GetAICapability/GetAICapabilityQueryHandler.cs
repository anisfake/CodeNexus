using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.AIConfigs.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.AIConfigs.Queries.GetAICapability;

public class GetAICapabilityQueryHandler : IRequestHandler<GetAICapabilityQuery, GetAICapabilityResponse>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISubscriptionAccessService _subscriptionAccessService;

    public GetAICapabilityQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ISubscriptionAccessService subscriptionAccessService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _subscriptionAccessService = subscriptionAccessService;
    }

    public async Task<GetAICapabilityResponse> Handle(GetAICapabilityQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var planExpiresAt = await _context.Users
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.PlanExpiresAt)
            .FirstOrDefaultAsync(cancellationToken);
        var effectivePlan = await _subscriptionAccessService.GetEffectivePlanAsync(userId, cancellationToken);

        var activeConfigs = await _context.AIProviderConfigs
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new { x.UsageType, x.AccessTier })
            .ToListAsync(cancellationToken);

        var capabilities = Enum.GetValues<AIUsageType>()
            .Select(usageType => new AICapabilityByUsageDto(
                usageType,
                activeConfigs.Any(x => x.UsageType == usageType && x.AccessTier == AIAccessTier.Free),
                activeConfigs.Any(x => x.UsageType == usageType && x.AccessTier == AIAccessTier.Paid)))
            .ToList();

        var isFreePlan = string.Equals(
            effectivePlan.PlanType.ToString(),
            SubscriptionPlanType.Free.ToString(),
            StringComparison.OrdinalIgnoreCase);

        return new GetAICapabilityResponse(
            HasPaidAccess: !isFreePlan,
            CurrentPlan: effectivePlan.Name,
            PlanExpiresAt: isFreePlan ? null : planExpiresAt,
            Capabilities: capabilities);
    }
}
