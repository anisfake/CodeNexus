using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.LearningPathMentorReviews.DTOs;

public record UpsertLearningPathMentorReviewRequest(
    int Score,
    string Feedback,
    string? Suggestions);

public record LearningPathMentorReviewDto(
    Guid ReviewId,
    Guid PathId,
    Guid MentorId,
    string MentorName,
    Guid StudentId,
    int Score,
    string Feedback,
    string? Suggestions,
    LearningPathMentorReviewDecisionStatus DecisionStatus,
    string? StudentDecisionNote,
    DateTime? StudentDecidedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record UpsertLearningPathMentorReviewResponseDto(
    Guid ReviewId,
    Guid PathId,
    Guid MentorId,
    Guid StudentId,
    int Score,
    string Feedback,
    string? Suggestions,
    LearningPathMentorReviewDecisionStatus DecisionStatus,
    string? StudentDecisionNote,
    DateTime? StudentDecidedAt,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    double AverageScore,
    int TotalReviews);

public record RespondLearningPathMentorReviewRequest(
    LearningPathMentorReviewDecisionStatus DecisionStatus,
    string? StudentDecisionNote);

public record RespondLearningPathMentorReviewResponseDto(
    Guid ReviewId,
    Guid PathId,
    LearningPathMentorReviewDecisionStatus DecisionStatus,
    string? StudentDecisionNote,
    DateTime? StudentDecidedAt);

public record LearningPathMentorReviewListResponseDto(
    Guid PathId,
    double AverageScore,
    int TotalReviews,
    List<LearningPathMentorReviewDto> Reviews);
