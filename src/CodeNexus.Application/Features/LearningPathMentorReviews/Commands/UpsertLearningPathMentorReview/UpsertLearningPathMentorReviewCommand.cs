using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathMentorReviews.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.LearningPathMentorReviews.Commands.UpsertLearningPathMentorReview;

public record UpsertLearningPathMentorReviewCommand(
    Guid PathId,
    int Score,
    string Feedback,
    string? Suggestions,
    string? ChangeSummary = null,
    string? ChangeReason = null)
    : IRequest<Result<UpsertLearningPathMentorReviewResponseDto>>;
