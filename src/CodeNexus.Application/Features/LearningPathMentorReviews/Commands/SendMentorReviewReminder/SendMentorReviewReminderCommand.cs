using CodeNexus.Application.Common.Models;
using MediatR;

namespace CodeNexus.Application.Features.LearningPathMentorReviews.Commands.SendMentorReviewReminder;

public record SendMentorReviewReminderCommand(Guid ReviewId) : IRequest<Result>;
