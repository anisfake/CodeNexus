namespace CodeNexus.Application.Features.StudentMentorSubscriptions.DTOs;

public record StudentMentorQuotaDto(
    Guid? SubscriptionId,
    Guid? PackageId,
    string? PackageName,
    bool HasActiveSubscription,
    int SharesFromMentorLimit,
    int SharesFromMentorUsed,
    int SharesFromMentorRemaining,
    int ValidationRequestLimit,
    int ValidationRequestsUsed,
    int ValidationRequestsRemaining,
    int TaskReviewLimit,
    int TaskReviewsUsed,
    int TaskReviewsRemaining
);
