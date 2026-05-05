using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathMentorReviews.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.LearningPathMentorReviews.Commands.UpsertLearningPathMentorReview;

public record UpsertLearningPathMentorReviewCommand(
    Guid PathId,
    string ChangeSummary,
    string ChangeReason)
    : IRequest<Result<UpsertLearningPathMentorReviewResponseDto>>;
