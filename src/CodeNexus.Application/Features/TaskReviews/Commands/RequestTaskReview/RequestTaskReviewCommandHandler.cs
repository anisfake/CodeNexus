using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Notifications.DTOs;
using CodeNexus.Application.Features.TaskReviews.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.TaskReviews.Commands.RequestTaskReview;

public class RequestTaskReviewCommandHandler
    : IRequestHandler<RequestTaskReviewCommand, Result<RequestTaskReviewResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly INotificationRealtimeNotifier _notificationNotifier;

    public RequestTaskReviewCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        INotificationRealtimeNotifier notificationNotifier)
    {
        _context = context;
        _currentUserService = currentUserService;
        _notificationNotifier = notificationNotifier;
    }

    public async Task<Result<RequestTaskReviewResponseDto>> Handle(
        RequestTaskReviewCommand request,
        CancellationToken cancellationToken)
    {
        Guid studentId;
        try
        {
            studentId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<RequestTaskReviewResponseDto>.Failure("UNAUTHORIZED", "User not authenticated.");
        }

        // Load the session and validate ownership
        var session = await _context.FocusSessions
            .Include(s => s.Task)
            .FirstOrDefaultAsync(s => s.SessionId == request.SessionId, cancellationToken);

        if (session == null)
        {
            return Result<RequestTaskReviewResponseDto>.Failure("SESSION_NOT_FOUND", "Focus session not found.");
        }

        if (session.Task.LearningPath == null)
        {
            // Load learning path for ownership check
            var taskWithPath = await _context.Tasks
                .AsNoTracking()
                .Include(t => t.LearningPath)
                .FirstOrDefaultAsync(t => t.TaskId == session.TaskId, cancellationToken);

            if (taskWithPath == null || taskWithPath.LearningPath.UserId != studentId)
            {
                return Result<RequestTaskReviewResponseDto>.Failure("ACCESS_DENIED", "You can only request review for your own sessions.");
            }
        }
        else if (session.Task.LearningPath.UserId != studentId)
        {
            return Result<RequestTaskReviewResponseDto>.Failure("ACCESS_DENIED", "You can only request review for your own sessions.");
        }

        // Ensure session has at least some content to review
        var hasSubmission = session.SubmittedCode != null
            || session.SubmittedSummary != null
            || session.SubmittedQuizAnswers != null;

        if (!hasSubmission)
        {
            return Result<RequestTaskReviewResponseDto>.Failure(
                "NO_SUBMISSION",
                "The session has no submitted content to review.");
        }

        // Ensure no existing review for this session
        var existingReview = await _context.TaskReviews
            .AsNoTracking()
            .AnyAsync(r => r.SessionId == request.SessionId, cancellationToken);

        if (existingReview)
        {
            return Result<RequestTaskReviewResponseDto>.Failure(
                "REVIEW_ALREADY_REQUESTED",
                "A review has already been requested for this session.");
        }

        // Find active subscription for the student
        var subscription = await _context.StudentMentorSubscriptions
            .FirstOrDefaultAsync(
                s => s.UserId == studentId && s.IsActive,
                cancellationToken);

        if (subscription == null)
        {
            return Result<RequestTaskReviewResponseDto>.Failure(
                "SUBSCRIPTION_NOT_FOUND",
                "No active mentor subscription found.");
        }

        // Check task review limit (-1 = unlimited)
        if (subscription.TaskReviewLimit != -1
            && subscription.TaskReviewsUsed >= subscription.TaskReviewLimit)
        {
            return Result<RequestTaskReviewResponseDto>.Failure(
                "TASK_REVIEW_LIMIT_REACHED",
                $"You have used all {subscription.TaskReviewLimit} task review(s) in your current subscription.");
        }

        // Find or create a direct conversation between student and mentor
        var conversation = await _context.DirectConversations
            .FirstOrDefaultAsync(
                c => (c.StudentId == studentId && c.MentorId == request.MentorId)
                     || (c.StudentId == request.MentorId && c.MentorId == studentId),
                cancellationToken);

        var now = DateTime.SpecifyKind(DateTime.UtcNow.AddHours(7), DateTimeKind.Unspecified);

        if (conversation == null)
        {
            conversation = new DirectConversation
            {
                ConversationId = NewId.NextGuid(),
                MentorId = request.MentorId,
                StudentId = studentId,
                ConversationType = ChatConversationType.Direct,
                CreatedAt = now
            };
            _context.DirectConversations.Add(conversation);
        }

        // Create the TaskReview record
        var review = new TaskReview
        {
            ReviewId = NewId.NextGuid(),
            SessionId = request.SessionId,
            TaskId = session.TaskId,
            StudentId = studentId,
            MentorId = request.MentorId,
            SubscriptionId = subscription.SubscriptionId,
            StudentRequestNote = string.IsNullOrWhiteSpace(request.StudentRequestNote)
                ? null
                : request.StudentRequestNote.Trim(),
            Status = TaskReviewStatus.Pending,
            RequestedAt = now
        };
        _context.TaskReviews.Add(review);

        // Mark the task as pending review
        session.Task.Status = TaskStatus_.PendingReview;

        // Create DirectMessage of type TaskReview
        var messageContent = string.IsNullOrWhiteSpace(request.StudentRequestNote)
            ? "Task review request"
            : request.StudentRequestNote.Trim();

        var message = new DirectMessage
        {
            MessageId = NewId.NextGuid(),
            ConversationId = conversation.ConversationId,
            SenderId = studentId,
            Content = messageContent,
            MessageType = DirectMessageType.TaskReview,
            TaskReviewId = review.ReviewId,
            SentAt = now
        };
        _context.DirectMessages.Add(message);

        // Receipt for mentor
        var receipt = new DirectMessageReceipt
        {
            ReceiptId = NewId.NextGuid(),
            MessageId = message.MessageId,
            UserId = request.MentorId
        };
        _context.DirectMessageReceipts.Add(receipt);

        // Update conversation last message
        conversation.LastMessagePreview = messageContent.Length > 120
            ? messageContent[..120]
            : messageContent;
        conversation.LastMessageAt = now;

        // Increment subscription usage
        subscription.TaskReviewsUsed += 1;

        // Create in-app notification for mentor
        var taskTitle = session.Task.Title;
        var notification = new Notification
        {
            NotificationId = NewId.NextGuid(),
            UserId = request.MentorId,
            Title = "notification.taskReviewRequested.title",
            Message = "notification.taskReviewRequested.message",
            Type = NotificationType.TaskReviewRequested,
            Severity = "Info",
            Channels = "Web,Main",
            TargetType = "taskReview",
            TargetId = review.ReviewId,
            Route = $"/task-reviews/{review.ReviewId}",
            TaskId = session.TaskId,
            IsRead = false,
            CreatedAt = now
        };
        _context.Notifications.Add(notification);

        await _context.SaveChangesAsync(cancellationToken);

        await _notificationNotifier.NotifyCreatedAsync(
            new[] { NotificationDtoMapper.ToDto(notification) },
            cancellationToken);

        return Result<RequestTaskReviewResponseDto>.Success(new RequestTaskReviewResponseDto(
            review.ReviewId,
            message.MessageId,
            conversation.ConversationId
        ));
    }
}
