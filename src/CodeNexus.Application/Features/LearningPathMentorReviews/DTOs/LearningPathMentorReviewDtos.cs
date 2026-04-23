using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.LearningPathMentorReviews.DTOs;

public record UpsertLearningPathMentorReviewRequest(
    string ChangeSummary,
    string ChangeReason);

public record RequestLearningPathMentorReviewRequest(
    Guid MentorId,
    string? StudentRequestNote);

public record LearningPathMentorReviewDto(
    Guid ReviewId,
    Guid PathId,
    Guid MentorId,
    string MentorName,
    Guid StudentId,
    LearningPathMentorReviewDecisionStatus DecisionStatus,
    string? StudentDecisionNote,
    DateTime? StudentDecidedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    Guid? RevisedPathId = null,
    string? StudentRequestNote = null,
    string? ChangeSummary = null,
    string? ChangeReason = null,
    int RejectionCount = 0,
    int MaxRejections = 3,
    bool CanRequestRevision = true);

public record UpsertLearningPathMentorReviewResponseDto(
    Guid ReviewId,
    Guid PathId,
    Guid MentorId,
    Guid StudentId,
    LearningPathMentorReviewDecisionStatus DecisionStatus,
    string? StudentDecisionNote,
    DateTime? StudentDecidedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    Guid? RevisedPathId = null,
    string? ChangeSummary = null,
    string? ChangeReason = null,
    int RejectionCount = 0,
    int MaxRejections = 3,
    bool CanRequestRevision = true);

public record RequestLearningPathMentorReviewResponseDto(
    Guid ReviewId,
    Guid PathId,
    Guid MentorId,
    Guid StudentId,
    Guid? RevisedPathId,
    string? StudentRequestNote,
    LearningPathMentorReviewDecisionStatus DecisionStatus,
    int RejectionCount,
    int MaxRejections,
    bool CanRequestRevision,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record RespondLearningPathMentorReviewRequest(
    LearningPathMentorReviewDecisionStatus DecisionStatus,
    string? StudentDecisionNote);

public record RespondLearningPathMentorReviewResponseDto(
    Guid ReviewId,
    Guid PathId,
    LearningPathMentorReviewDecisionStatus DecisionStatus,
    string? StudentDecisionNote,
    DateTime? StudentDecidedAt,
    int RejectionCount = 0,
    int MaxRejections = 3,
    bool CanRequestRevision = true);

public record LearningPathMentorReviewListResponseDto(
    Guid PathId,
    List<LearningPathMentorReviewDto> Reviews);
