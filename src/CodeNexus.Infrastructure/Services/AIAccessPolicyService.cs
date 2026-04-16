using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.AIAccessPolicy;
using CodeNexus.Application.Features.SystemRuntimePolicies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace CodeNexus.Infrastructure.Services;

public class AIAccessPolicyService : IAIAccessPolicyService
{
    private const int DefaultMentorPaidRequestsMonthlyLimit = 5000;
    private const int DefaultMentorDowngradeNotifyCooldownHours = 24;
    private readonly IConfiguration _configuration;
    private readonly IApplicationDbContext _context;

    public AIAccessPolicyService(IConfiguration configuration, IApplicationDbContext context)
    {
        _configuration = configuration;
        _context = context;
    }

    public async Task<int> GetMentorPaidRequestsMonthlyLimitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var policy = await _context.SystemRuntimePolicies
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.PolicyKey == MentorAiAccessPolicyConstants.PolicyKey && x.IsActive,
                    cancellationToken);

            if (policy != null)
            {
                var parsed = ReadPolicyInt(policy.ConfigJson, MentorAiAccessPolicyConstants.MonthlyLimitConfigKey);
                if (parsed.HasValue && parsed.Value >= 0)
                {
                    return parsed.Value;
                }
            }
        }
        catch
        {
            // Fallback when migration has not been applied yet.
        }

        var rawValue = _configuration["AIAccess:MentorPaidRequestsMonthlyLimit"];
        if (int.TryParse(rawValue, out var parsedFromConfig) && parsedFromConfig >= 0)
        {
            return parsedFromConfig;
        }

        return DefaultMentorPaidRequestsMonthlyLimit;
    }

    public async Task<int> GetMentorDowngradeNotifyCooldownHoursAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var policy = await _context.SystemRuntimePolicies
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.PolicyKey == MentorAiAccessPolicyConstants.PolicyKey && x.IsActive,
                    cancellationToken);

            if (policy != null)
            {
                var parsed = ReadPolicyInt(policy.ConfigJson, MentorAiAccessPolicyConstants.CooldownHoursConfigKey);
                if (parsed.HasValue && parsed.Value > 0)
                {
                    return parsed.Value;
                }
            }
        }
        catch
        {
            // Fallback when migration has not been applied yet.
        }

        var rawValue = _configuration["AIAccess:MentorDowngradeNotifyCooldownHours"];
        if (int.TryParse(rawValue, out var parsedFromConfig) && parsedFromConfig > 0)
        {
            return parsedFromConfig;
        }

        return DefaultMentorDowngradeNotifyCooldownHours;
    }

    private static int? ReadPolicyInt(string? configJson, string key)
    {
        var config = SystemRuntimePolicyJsonHelper.ParseConfigJson(configJson);
        if (!config.TryGetValue(key, out var rawValue))
        {
            return null;
        }

        return rawValue switch
        {
            int intValue => intValue,
            long longValue when longValue is <= int.MaxValue and >= int.MinValue => (int)longValue,
            double doubleValue when doubleValue is <= int.MaxValue and >= int.MinValue => (int)doubleValue,
            decimal decimalValue when decimalValue is <= int.MaxValue and >= int.MinValue => (int)decimalValue,
            string text when int.TryParse(text, out var parsed) => parsed,
            _ => null
        };
    }
}
