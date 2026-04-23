using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathMentorReviews.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.LearningPathMentorReviews.Commands.RequestLearningPathMentorReview;

public record RequestLearningPathMentorReviewCommand(
    Guid PathId,
    Guid MentorId,
    string? StudentRequestNote,
    int? MaxRejectCount = null)
    : IRequest<Result<RequestLearningPathMentorReviewResponseDto>>;
