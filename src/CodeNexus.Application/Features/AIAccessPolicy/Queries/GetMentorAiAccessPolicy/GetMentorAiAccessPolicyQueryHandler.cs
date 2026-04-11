using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.AIAccessPolicy;
using CodeNexus.Application.Features.AIAccessPolicy.DTOs;
using CodeNexus.Application.Features.SystemRuntimePolicies;
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
        var policy = await _context.SystemRuntimePolicies
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.PolicyKey == MentorAiAccessPolicyConstants.PolicyKey, cancellationToken);

        if (policy != null)
        {
            var config = SystemRuntimePolicyJsonHelper.ParseConfigJson(policy.ConfigJson);
            var policyMonthlyLimit = ReadIntOrNull(config, MentorAiAccessPolicyConstants.MonthlyLimitConfigKey)
                ?? await _aiAccessPolicyService.GetMentorPaidRequestsMonthlyLimitAsync(cancellationToken);
            var policyCooldownHours = ReadIntOrNull(config, MentorAiAccessPolicyConstants.CooldownHoursConfigKey)
                ?? await _aiAccessPolicyService.GetMentorDowngradeNotifyCooldownHoursAsync(cancellationToken);

            return Result<MentorAiAccessPolicyDto>.Success(new MentorAiAccessPolicyDto(
                policyMonthlyLimit,
                policyCooldownHours,
                policy.UpdatedAt));
        }

        var monthlyLimit = await _aiAccessPolicyService.GetMentorPaidRequestsMonthlyLimitAsync(cancellationToken);
        var cooldownHours = await _aiAccessPolicyService.GetMentorDowngradeNotifyCooldownHoursAsync(cancellationToken);

        return Result<MentorAiAccessPolicyDto>.Success(new MentorAiAccessPolicyDto(
            monthlyLimit,
            cooldownHours,
            DateTime.UtcNow));
    }

    private static int? ReadIntOrNull(Dictionary<string, object> config, string key)
    {
        if (!config.TryGetValue(key, out var raw))
        {
            return null;
        }

        return raw switch
        {
            int value => value,
            long value when value <= int.MaxValue && value >= int.MinValue => (int)value,
            double value when value <= int.MaxValue && value >= int.MinValue => (int)value,
            decimal value when value <= int.MaxValue && value >= int.MinValue => (int)value,
            string text when int.TryParse(text, out var parsed) => parsed,
            _ => null
        };
    }
}
