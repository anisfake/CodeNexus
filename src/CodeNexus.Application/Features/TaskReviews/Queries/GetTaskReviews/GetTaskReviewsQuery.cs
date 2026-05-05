using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.TaskReviews.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.TaskReviews.Queries.GetTaskReviews;

public record GetTaskReviewsQuery(
    string? Status = "Pending",
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<Result<PaginationDto<TaskReviewListItemDto>>>;
