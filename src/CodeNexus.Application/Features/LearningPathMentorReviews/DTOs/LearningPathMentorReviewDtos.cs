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
    int ValidationRequestsUsed = 0,
    int ValidationRequestLimit = 0,
    bool CanRequestValidation = true);

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
    int ValidationRequestsUsed = 0,
    int ValidationRequestLimit = 0,
    bool CanRequestValidation = true);

public record RequestLearningPathMentorReviewResponseDto(
    Guid ReviewId,
    Guid PathId,
    Guid MentorId,
    Guid StudentId,
    Guid? RevisedPathId,
    string? StudentRequestNote,
    LearningPathMentorReviewDecisionStatus DecisionStatus,
    int ValidationRequestsUsed,
    int ValidationRequestLimit,
    bool CanRequestValidation,
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
    int ValidationRequestsUsed = 0,
    int ValidationRequestLimit = 0,
    bool CanRequestValidation = true);

public record LearningPathMentorReviewListResponseDto(
    Guid PathId,
    List<LearningPathMentorReviewDto> Reviews);

public record AdminMentorReviewDto(
    Guid ReviewId,
    Guid PathId,
    string PathTitle,
    Guid StudentId,
    string StudentName,
    string StudentEmail,
    Guid MentorId,
    string MentorName,
    string MentorEmail,
    LearningPathMentorReviewDecisionStatus DecisionStatus,
    string? StudentRequestNote,
    string? ChangeSummary,
    string? ChangeReason,
    string? StudentDecisionNote,
    DateTime? StudentDecidedAt,
    DateTime? MentorRespondedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

