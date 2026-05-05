using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.TaskReviews.DTOs;

public record TaskReviewDto(
    Guid ReviewId,
    Guid SessionId,
    Guid TaskId,
    string TaskTitle,
    Guid StudentId,
    string StudentUserName,
    Guid MentorId,
    string MentorUserName,
    int? Score,
    string? Feedback,
    string? Suggestions,
    string? StudentRequestNote,
    string Status,
    DateTime RequestedAt,
    DateTime? ReviewedAt,
    // Session submission snapshot
    string? SubmittedCode,
    string? SubmittedSummary,
    string? SubmittedQuizAnswers,
    string? AIFeedback,
    int? VerificationScore,
    bool IsVerified
);

public record RequestTaskReviewResponseDto(
    Guid ReviewId,
    Guid MessageId,
    Guid ConversationId
);

public record TaskReviewInfoDto(
    Guid ReviewId,
    Guid MentorId,
    string MentorUserName,
    int? Score,
    string? Feedback,
    string? Suggestions,
    string Status,
    DateTime RequestedAt,
    DateTime? ReviewedAt
);

public record TaskReviewListItemDto(
    Guid ReviewId,
    Guid SessionId,
    Guid TaskId,
    string TaskTitle,
    Guid StudentId,
    string StudentUserName,
    string? StudentAvatarUrl,
    Guid MentorId,
    string MentorUserName,
    string? MentorAvatarUrl,
    int? Score,
    string? StudentRequestNote,
    string Status,
    DateTime RequestedAt,
    DateTime? ReviewedAt
);
