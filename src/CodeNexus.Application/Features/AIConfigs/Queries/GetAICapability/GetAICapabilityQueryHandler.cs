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

    public GetAICapabilityQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<GetAICapabilityResponse> Handle(GetAICapabilityQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var userAccess = await _context.Users
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => new
            {
                RoleName = x.Role != null ? x.Role.RoleName : string.Empty,
                x.TokenBalance
            })
            .FirstOrDefaultAsync(cancellationToken);

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

        var hasPaidAccess = string.Equals(userAccess?.RoleName, "Admin", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(userAccess?.RoleName, "Mentor", StringComparison.OrdinalIgnoreCase)
                            || (userAccess?.TokenBalance ?? 0m) > 0m;

        return new GetAICapabilityResponse(
            HasPaidAccess: hasPaidAccess,
            CurrentPlan: "Token Billing",
            PlanExpiresAt: null,
            Capabilities: capabilities);
    }
}

