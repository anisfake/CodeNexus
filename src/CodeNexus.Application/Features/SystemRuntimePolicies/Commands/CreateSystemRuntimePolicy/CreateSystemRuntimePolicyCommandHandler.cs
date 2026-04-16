using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.SystemRuntimePolicies.DTOs;
using CodeNexus.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MassTransit;

namespace CodeNexus.Application.Features.SystemRuntimePolicies.Commands.CreateSystemRuntimePolicy;

public class CreateSystemRuntimePolicyCommandHandler
    : IRequestHandler<CreateSystemRuntimePolicyCommand, Result<SystemRuntimePolicyDto>>
{
    private readonly IApplicationDbContext _context;

    public CreateSystemRuntimePolicyCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<SystemRuntimePolicyDto>> Handle(
        CreateSystemRuntimePolicyCommand request,
        CancellationToken cancellationToken)
    {
        var normalizedPolicyKey = request.PolicyKey.Trim();

        var exists = await _context.SystemRuntimePolicies
            .AsNoTracking()
            .AnyAsync(x => x.PolicyKey == normalizedPolicyKey, cancellationToken);

        if (exists)
        {
            return Result<SystemRuntimePolicyDto>.Failure(
                "POLICY_ALREADY_EXISTS",
                "System runtime policy key already exists.");
        }

        var now = DateTime.UtcNow;
        var entity = new SystemRuntimePolicy
        {
            SystemRuntimePolicyId = NewId.NextGuid(),
            PolicyKey = normalizedPolicyKey,
            Description = request.Description?.Trim(),
            ConfigJson = SystemRuntimePolicyJsonHelper.SerializeConfigJson(request.ConfigJson),
            IsActive = request.IsActive,
            UpdatedAt = now
        };

        await _context.SystemRuntimePolicies.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<SystemRuntimePolicyDto>.Success(new SystemRuntimePolicyDto(
            entity.SystemRuntimePolicyId,
            entity.PolicyKey,
            entity.Description,
            SystemRuntimePolicyJsonHelper.ParseConfigJson(entity.ConfigJson),
            entity.IsActive,
            entity.UpdatedAt));
    }
}

