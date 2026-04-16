using CodeNexus.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Infrastructure.Services;

public class SubscriptionAccessService : ISubscriptionAccessService
{
    private readonly IApplicationDbContext _context;

    public SubscriptionAccessService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> CanUsePersonalGoalsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (await IsPlanLimitExemptRoleAsync(userId, cancellationToken))
        {
            return true;
        }

        return await HasPositiveBalanceAsync(userId, cancellationToken);
    }

    public async Task<bool> CanUsePaidModelsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (await IsPlanLimitExemptRoleAsync(userId, cancellationToken))
        {
            return true;
        }

        return await HasPositiveBalanceAsync(userId, cancellationToken);
    }

    private async Task<bool> IsPlanLimitExemptRoleAsync(Guid userId, CancellationToken cancellationToken)
    {
        var roleName = await _context.Users
            .AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => u.Role != null ? u.Role.RoleName : null)
            .FirstOrDefaultAsync(cancellationToken);

        return string.Equals(roleName, "Mentor", StringComparison.OrdinalIgnoreCase)
               || string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> HasPositiveBalanceAsync(Guid userId, CancellationToken cancellationToken)
    {
        var balance = await _context.Users
            .AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => (decimal?)u.TokenBalance)
            .FirstOrDefaultAsync(cancellationToken);

        return (balance ?? 0m) > 0m;
    }
}

