using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathMentorReviews.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.LearningPathMentorReviews.Queries.GetLearningPathMentorReviews;

public record GetLearningPathMentorReviewsQuery(Guid PathId)
    : IRequest<Result<LearningPathMentorReviewListResponseDto>>;

