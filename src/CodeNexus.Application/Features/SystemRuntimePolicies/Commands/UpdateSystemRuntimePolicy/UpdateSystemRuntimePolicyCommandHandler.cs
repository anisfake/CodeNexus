using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.SystemRuntimePolicies;
using CodeNexus.Application.Features.SystemRuntimePolicies.DTOs;
using CodeNexus.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.SystemRuntimePolicies.Commands.UpdateSystemRuntimePolicy;

public class UpdateSystemRuntimePolicyCommandHandler : IRequestHandler<UpdateSystemRuntimePolicyCommand, Result<SystemRuntimePolicyDto>>
{
    private readonly IApplicationDbContext _context;

    public UpdateSystemRuntimePolicyCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<SystemRuntimePolicyDto>> Handle(UpdateSystemRuntimePolicyCommand request, CancellationToken cancellationToken)
    {
        var normalizedPolicyKey = request.PolicyKey.Trim();

        var policy = await _context.SystemRuntimePolicies
            .FirstOrDefaultAsync(x => x.PolicyKey == normalizedPolicyKey, cancellationToken);

        if (policy == null)
        {
            policy = new SystemRuntimePolicy
            {
                SystemRuntimePolicyId = Guid.NewGuid(),
                PolicyKey = normalizedPolicyKey
            };
            await _context.SystemRuntimePolicies.AddAsync(policy, cancellationToken);
        }

        policy.PolicyKey = normalizedPolicyKey;
        policy.Description = request.Description?.Trim();
        policy.ConfigJson = SystemRuntimePolicyJsonHelper.SerializeConfigJson(request.ConfigJson);
        policy.IsActive = request.IsActive;
        policy.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Result<SystemRuntimePolicyDto>.Success(new SystemRuntimePolicyDto(
            policy.SystemRuntimePolicyId,
            policy.PolicyKey,
            policy.Description,
            SystemRuntimePolicyJsonHelper.ParseConfigJson(policy.ConfigJson),
            policy.IsActive,
            policy.UpdatedAt));
    }
}
