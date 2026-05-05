using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathMentorReviews.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;

namespace CodeNexus.Application.Features.LearningPathMentorReviews.Queries.GetMyStudentLearningPathReviews;

public record GetMyStudentLearningPathReviewsQuery(
    LearningPathMentorReviewDecisionStatus? Status = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<Result<List<AdminMentorReviewDto>>>;
