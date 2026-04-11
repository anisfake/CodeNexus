using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.AIAccessPolicy;
using CodeNexus.Application.Features.AIAccessPolicy.DTOs;
using CodeNexus.Application.Features.SystemRuntimePolicies;
using CodeNexus.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using MassTransit;

namespace CodeNexus.Application.Features.AIAccessPolicy.Commands.UpdateMentorAiAccessPolicy;

public class UpdateMentorAiAccessPolicyCommandHandler : IRequestHandler<UpdateMentorAiAccessPolicyCommand, Result<MentorAiAccessPolicyDto>>
{
    private readonly IApplicationDbContext _context;

    public UpdateMentorAiAccessPolicyCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<MentorAiAccessPolicyDto>> Handle(UpdateMentorAiAccessPolicyCommand request, CancellationToken cancellationToken)
    {
        var policy = await _context.SystemRuntimePolicies
            .FirstOrDefaultAsync(x => x.PolicyKey == MentorAiAccessPolicyConstants.PolicyKey, cancellationToken);

        if (policy == null)
        {
            policy = new SystemRuntimePolicy
            {
                SystemRuntimePolicyId = NewId.NextGuid(),
                PolicyKey = MentorAiAccessPolicyConstants.PolicyKey
            };

            await _context.SystemRuntimePolicies.AddAsync(policy, cancellationToken);
        }

        var config = SystemRuntimePolicyJsonHelper.ParseConfigJson(policy.ConfigJson);
        config[MentorAiAccessPolicyConstants.MonthlyLimitConfigKey] = request.MentorPaidRequestsMonthlyLimit;
        config[MentorAiAccessPolicyConstants.CooldownHoursConfigKey] = request.MentorDowngradeNotifyCooldownHours;

        policy.Description = "Mentor AI access policy";
        policy.ConfigJson = SystemRuntimePolicyJsonHelper.SerializeConfigJson(config);
        policy.IsActive = true;
        policy.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Result<MentorAiAccessPolicyDto>.Success(new MentorAiAccessPolicyDto(
            request.MentorPaidRequestsMonthlyLimit,
            request.MentorDowngradeNotifyCooldownHours,
            policy.UpdatedAt));
    }
}
