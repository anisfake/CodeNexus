using CodeNexus.Application.Features.SystemRuntimePolicies.DTOs;

namespace CodeNexus.Application.Common.Interfaces;

public sealed record RuntimeOperationalPolicy(
    int FocusSessionAutoPauseAfterMinutes,
    int FocusSessionAutoAbandonAfterMinutes,
    int FocusSessionMonitorIntervalSeconds,
    int PendingPaymentTimeoutMinutes,
    int PendingPaymentMonitorIntervalSeconds,
    int OverdueNotificationIntervalMinutes,
    int MentorReviewReminderAfterDays,
    int StudentResponseReminderAfterDays
);

public interface ISystemRuntimePolicyService
{
    Task<SystemRuntimePolicyDto?> GetPolicyAsync(string policyKey, CancellationToken cancellationToken = default);
    Task<RuntimeOperationalPolicy> GetRuntimeOperationalPolicyAsync(CancellationToken cancellationToken = default);
}
