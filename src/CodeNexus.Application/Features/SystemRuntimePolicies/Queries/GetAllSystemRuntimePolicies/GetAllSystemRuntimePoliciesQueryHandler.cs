using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.SystemRuntimePolicies.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.SystemRuntimePolicies.Queries.GetAllSystemRuntimePolicies;

public class GetAllSystemRuntimePoliciesQueryHandler : IRequestHandler<GetAllSystemRuntimePoliciesQuery, Result<List<SystemRuntimePolicyDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetAllSystemRuntimePoliciesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<SystemRuntimePolicyDto>>> Handle(GetAllSystemRuntimePoliciesQuery request, CancellationToken cancellationToken)
    {
        var items = await _context.SystemRuntimePolicies
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => new SystemRuntimePolicyDto(
                x.SystemRuntimePolicyId,
                x.PolicyKey,
                x.Description,
                SystemRuntimePolicyJsonHelper.ParseConfigJson(x.ConfigJson),
                x.IsActive,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Result<List<SystemRuntimePolicyDto>>.Success(items);
    }
}
