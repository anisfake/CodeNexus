using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathMentorReviews.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;

namespace CodeNexus.Application.Features.LearningPathMentorReviews.Commands.RespondLearningPathMentorReview;

public record RespondLearningPathMentorReviewCommand(
    Guid PathId,
    Guid ReviewId,
    LearningPathMentorReviewDecisionStatus DecisionStatus,
    string? StudentDecisionNote)
    : IRequest<Result<RespondLearningPathMentorReviewResponseDto>>;
