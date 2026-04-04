using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.AIAccessPolicy.DTOs;
using CodeNexus.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

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
        var policy = await _context.MentorAiAccessPolicies
            .FirstOrDefaultAsync(cancellationToken);

        if (policy == null)
        {
            policy = new MentorAiAccessPolicy
            {
                MentorAiAccessPolicyId = Guid.NewGuid(),
                MentorPaidRequestsMonthlyLimit = request.MentorPaidRequestsMonthlyLimit,
                MentorDowngradeNotifyCooldownHours = request.MentorDowngradeNotifyCooldownHours,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.MentorAiAccessPolicies.AddAsync(policy, cancellationToken);
        }
        else
        {
            policy.MentorPaidRequestsMonthlyLimit = request.MentorPaidRequestsMonthlyLimit;
            policy.MentorDowngradeNotifyCooldownHours = request.MentorDowngradeNotifyCooldownHours;
            policy.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result<MentorAiAccessPolicyDto>.Success(new MentorAiAccessPolicyDto(
            policy.MentorPaidRequestsMonthlyLimit,
            policy.MentorDowngradeNotifyCooldownHours,
            policy.UpdatedAt));
    }
}

