using CodeNexus.Application.Common.Models;
using MediatR;

namespace CodeNexus.Application.Features.TaskReviews.Commands.SubmitTaskReview;

public record SubmitTaskReviewCommand(
    Guid ReviewId,
    int Score,
    string Feedback,
    string? Suggestions)
    : IRequest<Result>;
