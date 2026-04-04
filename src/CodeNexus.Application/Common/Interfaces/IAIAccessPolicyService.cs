namespace CodeNexus.Application.Common.Interfaces;

public interface IAIAccessPolicyService
{
    Task<int> GetMentorPaidRequestsMonthlyLimitAsync(CancellationToken cancellationToken = default);
    Task<int> GetMentorDowngradeNotifyCooldownHoursAsync(CancellationToken cancellationToken = default);
}
