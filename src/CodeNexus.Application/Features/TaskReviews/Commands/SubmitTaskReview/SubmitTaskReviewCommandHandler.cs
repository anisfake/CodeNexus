using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Notifications.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.TaskReviews.Commands.SubmitTaskReview;

public class SubmitTaskReviewCommandHandler : IRequestHandler<SubmitTaskReviewCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly INotificationRealtimeNotifier _notificationNotifier;

    public SubmitTaskReviewCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        INotificationRealtimeNotifier notificationNotifier)
    {
        _context = context;
        _currentUserService = currentUserService;
        _notificationNotifier = notificationNotifier;
    }

    public async Task<Result> Handle(SubmitTaskReviewCommand request, CancellationToken cancellationToken)
    {
        Guid mentorId;
        try
        {
            mentorId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result.Failure("UNAUTHORIZED", "User not authenticated.");
        }

        var review = await _context.TaskReviews
            .Include(r => r.Session)
                .ThenInclude(s => s.Task)
            .FirstOrDefaultAsync(r => r.ReviewId == request.ReviewId, cancellationToken);

        if (review == null)
        {
            return Result.Failure("REVIEW_NOT_FOUND", "Task review not found.");
        }

        if (review.MentorId != mentorId)
        {
            return Result.Failure("UNAUTHORIZED", "Only the assigned mentor can submit this review.");
        }

        if (review.Status == TaskReviewStatus.Reviewed)
        {
            return Result.Failure("REVIEW_ALREADY_SUBMITTED", "This review has already been submitted.");
        }

        var now = DateTime.SpecifyKind(DateTime.UtcNow.AddHours(7), DateTimeKind.Unspecified);

        review.Score = request.Score;
        review.Feedback = request.Feedback.Trim();
        review.Suggestions = string.IsNullOrWhiteSpace(request.Suggestions)
            ? null
            : request.Suggestions.Trim();
        review.Status = TaskReviewStatus.Reviewed;
        review.ReviewedAt = now;

        // Create in-app notification for student
        var notification = new Notification
        {
            NotificationId = NewId.NextGuid(),
            UserId = review.StudentId,
            Title = "notification.taskReviewCompleted.title",
            Message = "notification.taskReviewCompleted.message",
            Type = NotificationType.TaskReviewCompleted,
            Severity = "Info",
            Channels = "Web,Main",
            TargetType = "taskReview",
            TargetId = review.ReviewId,
            Route = $"/focus-sessions/history",
            TaskId = review.TaskId,
            IsRead = false,
            CreatedAt = now
        };
        _context.Notifications.Add(notification);

        await _context.SaveChangesAsync(cancellationToken);

        await _notificationNotifier.NotifyCreatedAsync(
            new[] { NotificationDtoMapper.ToDto(notification) },
            cancellationToken);

        return Result.Success();
    }
}
