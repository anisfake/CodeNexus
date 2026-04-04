using CodeNexus.Application.Common.Interfaces;
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
            var policy = await _context.MentorAiAccessPolicies
                .AsNoTracking()
                .OrderByDescending(x => x.UpdatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (policy != null && policy.MentorPaidRequestsMonthlyLimit >= 0)
            {
                return policy.MentorPaidRequestsMonthlyLimit;
            }
        }
        catch
        {
            // Fallback when migration has not been applied yet.
        }

        var rawValue = _configuration["AIAccess:MentorPaidRequestsMonthlyLimit"];
        if (int.TryParse(rawValue, out var parsed) && parsed >= 0)
        {
            return parsed;
        }

        return DefaultMentorPaidRequestsMonthlyLimit;
    }

    public async Task<int> GetMentorDowngradeNotifyCooldownHoursAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var policy = await _context.MentorAiAccessPolicies
                .AsNoTracking()
                .OrderByDescending(x => x.UpdatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (policy != null && policy.MentorDowngradeNotifyCooldownHours > 0)
            {
                return policy.MentorDowngradeNotifyCooldownHours;
            }
        }
        catch
        {
            // Fallback when migration has not been applied yet.
        }

        var rawValue = _configuration["AIAccess:MentorDowngradeNotifyCooldownHours"];
        if (int.TryParse(rawValue, out var parsed) && parsed > 0)
        {
            return parsed;
        }

        return DefaultMentorDowngradeNotifyCooldownHours;
    }
}
