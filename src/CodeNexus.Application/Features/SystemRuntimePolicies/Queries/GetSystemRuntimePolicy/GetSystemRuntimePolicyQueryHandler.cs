using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.SystemRuntimePolicies.DTOs;
using CodeNexus.Application.Features.SystemRuntimePolicies;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.SystemRuntimePolicies.Queries.GetSystemRuntimePolicy;

public class GetSystemRuntimePolicyQueryHandler : IRequestHandler<GetSystemRuntimePolicyQuery, Result<SystemRuntimePolicyDto>>
{
    private readonly IApplicationDbContext _context;

    public GetSystemRuntimePolicyQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<SystemRuntimePolicyDto>> Handle(GetSystemRuntimePolicyQuery request, CancellationToken cancellationToken)
    {
        var normalizedPolicyKey = request.PolicyKey.Trim();

        var policy = await _context.SystemRuntimePolicies
            .AsNoTracking()
            .Where(x => x.PolicyKey == normalizedPolicyKey)
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (policy == null)
        {
            return Result<SystemRuntimePolicyDto>.Success(new SystemRuntimePolicyDto(
                null,
                normalizedPolicyKey,
                null,
                new Dictionary<string, object>(),
                true,
                DateTime.UtcNow));
        }

        return Result<SystemRuntimePolicyDto>.Success(new SystemRuntimePolicyDto(
            policy.SystemRuntimePolicyId,
            policy.PolicyKey,
            policy.Description,
            SystemRuntimePolicyJsonHelper.ParseConfigJson(policy.ConfigJson),
            policy.IsActive,
            policy.UpdatedAt));
    }
}
