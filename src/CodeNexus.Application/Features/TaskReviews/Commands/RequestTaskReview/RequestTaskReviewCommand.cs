using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.TaskReviews.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.TaskReviews.Commands.RequestTaskReview;

public record RequestTaskReviewCommand(
    Guid SessionId,
    Guid MentorId,
    string? StudentRequestNote)
    : IRequest<Result<RequestTaskReviewResponseDto>>;
