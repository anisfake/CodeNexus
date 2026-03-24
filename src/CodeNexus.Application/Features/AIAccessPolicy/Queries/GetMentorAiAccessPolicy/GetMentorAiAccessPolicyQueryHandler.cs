using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.AIAccessPolicy.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.AIAccessPolicy.Queries.GetMentorAiAccessPolicy;

public class GetMentorAiAccessPolicyQueryHandler : IRequestHandler<GetMentorAiAccessPolicyQuery, Result<MentorAiAccessPolicyDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IAIAccessPolicyService _aiAccessPolicyService;

    public GetMentorAiAccessPolicyQueryHandler(
        IApplicationDbContext context,
        IAIAccessPolicyService aiAccessPolicyService)
    {
        _context = context;
        _aiAccessPolicyService = aiAccessPolicyService;
    }

    public async Task<Result<MentorAiAccessPolicyDto>> Handle(GetMentorAiAccessPolicyQuery request, CancellationToken cancellationToken)
    {
        var policy = await _context.MentorAiAccessPolicies
            .AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (policy != null)
        {
            return Result<MentorAiAccessPolicyDto>.Success(new MentorAiAccessPolicyDto(
                policy.MentorPaidRequestsMonthlyLimit,
                policy.MentorDowngradeNotifyCooldownHours,
                policy.UpdatedAt));
        }

        var monthlyLimit = await _aiAccessPolicyService.GetMentorPaidRequestsMonthlyLimitAsync(cancellationToken);
        var cooldownHours = await _aiAccessPolicyService.GetMentorDowngradeNotifyCooldownHoursAsync(cancellationToken);

        return Result<MentorAiAccessPolicyDto>.Success(new MentorAiAccessPolicyDto(
            monthlyLimit,
            cooldownHours,
            DateTime.UtcNow));
    }
}

