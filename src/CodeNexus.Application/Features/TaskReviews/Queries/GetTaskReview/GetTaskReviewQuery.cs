using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.TaskReviews.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.TaskReviews.Queries.GetTaskReview;

public record GetTaskReviewQuery(Guid ReviewId) : IRequest<Result<TaskReviewDto>>;
