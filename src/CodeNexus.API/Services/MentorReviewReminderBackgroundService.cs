using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Notifications.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.API.Services;

public class MentorReviewReminderBackgroundService : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MentorReviewReminderBackgroundService> _logger;

    public MentorReviewReminderBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<MentorReviewReminderBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(InitialDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingRemindersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MentorReviewReminderBackgroundService crashed.");
            }

            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ProcessPendingRemindersAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var realtimeNotifier = scope.ServiceProvider.GetRequiredService<INotificationRealtimeNotifier>();
        var policyService = scope.ServiceProvider.GetRequiredService<ISystemRuntimePolicyService>();

        var policy = await policyService.GetRuntimeOperationalPolicyAsync(cancellationToken);

        await SendMentorRemindersAsync(context, emailService, realtimeNotifier,
            TimeSpan.FromDays(policy.MentorReviewReminderAfterDays), cancellationToken);

        await SendStudentRemindersAsync(context, emailService, realtimeNotifier,
            TimeSpan.FromDays(policy.StudentResponseReminderAfterDays), cancellationToken);
    }

    private async Task SendMentorRemindersAsync(
        IApplicationDbContext context,
        IEmailService emailService,
        INotificationRealtimeNotifier realtimeNotifier,
        TimeSpan threshold,
        CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.Subtract(threshold);

        var overdueReviews = await context.LearningPathMentorReviews
            .Include(r => r.Student)
            .Include(r => r.Mentor)
            .Include(r => r.LearningPath)
            .Where(r =>
                r.DecisionStatus == LearningPathMentorReviewDecisionStatus.Pending
                && r.CreatedAt <= cutoff)
            .ToListAsync(cancellationToken);

        foreach (var review in overdueReviews)
        {
            try
            {
                var mentorName = review.Mentor.FirstName ?? review.Mentor.Username;
                var studentName = review.Student.FirstName ?? review.Student.Username;
                var pathTitle = review.LearningPath.Title;

                var subject = "Nhắc nhở: Lộ trình học đang chờ bạn review";
                var message = $"Xin chào {mentorName},\n\n" +
                              $"Student {studentName} đang chờ bạn review lộ trình \"{pathTitle}\".\n\n" +
                              $"Vui lòng đăng nhập vào hệ thống để xem và phản hồi yêu cầu review.\n\n" +
                              $"Trân trọng,\nCodeNexus";

                await emailService.SendNotificationEmailAsync(review.Mentor.Email, subject, message, cancellationToken);

                var notification = new Notification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId = review.MentorId,
                    Type = NotificationType.Reminder,
                    Title = "Lộ trình học đang chờ bạn review",
                    Message = $"Student {studentName} đang chờ bạn review lộ trình \"{pathTitle}\".",
                    Severity = "Warning",
                    Channels = "Web",
                    LearningPathId = review.PathId,
                    NotifiedPathTitle = pathTitle,
                    CreatedAt = DateTime.UtcNow
                };

                context.Notifications.Add(notification);
                await context.SaveChangesAsync(cancellationToken);

                await realtimeNotifier.NotifyCreatedAsync(
                    [NotificationDtoMapper.ToDto(notification)],
                    cancellationToken);

                _logger.LogInformation(
                    "Sent review reminder to mentor {MentorId} for review {ReviewId}.",
                    review.MentorId, review.ReviewId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send mentor reminder for review {ReviewId}.", review.ReviewId);
            }
        }
    }

    private async Task SendStudentRemindersAsync(
        IApplicationDbContext context,
        IEmailService emailService,
        INotificationRealtimeNotifier realtimeNotifier,
        TimeSpan threshold,
        CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.Subtract(threshold);

        var overdueReviews = await context.LearningPathMentorReviews
            .Include(r => r.Student)
            .Include(r => r.Mentor)
            .Include(r => r.LearningPath)
            .Where(r =>
                r.DecisionStatus == LearningPathMentorReviewDecisionStatus.WaitingStudentResponse
                && r.MentorRespondedAt.HasValue
                && r.MentorRespondedAt.Value <= cutoff)
            .ToListAsync(cancellationToken);

        foreach (var review in overdueReviews)
        {
            try
            {
                var studentName = review.Student.FirstName ?? review.Student.Username;
                var mentorName = review.Mentor.FirstName ?? review.Mentor.Username;
                var pathTitle = review.LearningPath.Title;

                var subject = "Nhắc nhở: Lộ trình học của bạn đang chờ phản hồi";
                var message = $"Xin chào {studentName},\n\n" +
                              $"Mentor {mentorName} đã hoàn thành review lộ trình \"{pathTitle}\" của bạn và đang chờ bạn phản hồi.\n\n" +
                              $"Vui lòng đăng nhập vào hệ thống để xem nhận xét và chấp nhận hoặc từ chối đề xuất của mentor.\n\n" +
                              $"Trân trọng,\nCodeNexus";

                await emailService.SendNotificationEmailAsync(review.Student.Email, subject, message, cancellationToken);

                var notification = new Notification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId = review.StudentId,
                    Type = NotificationType.Reminder,
                    Title = "Lộ trình học đang chờ phản hồi",
                    Message = $"Mentor {mentorName} đã review lộ trình \"{pathTitle}\". Vui lòng xác nhận hoặc từ chối đề xuất.",
                    Severity = "Warning",
                    Channels = "Web",
                    LearningPathId = review.PathId,
                    NotifiedPathTitle = pathTitle,
                    NotifiedMentorUserName = review.Mentor.Username,
                    CreatedAt = DateTime.UtcNow
                };

                context.Notifications.Add(notification);
                await context.SaveChangesAsync(cancellationToken);

                await realtimeNotifier.NotifyCreatedAsync(
                    [NotificationDtoMapper.ToDto(notification)],
                    cancellationToken);

                _logger.LogInformation(
                    "Sent review reminder to student {StudentId} for review {ReviewId}.",
                    review.StudentId, review.ReviewId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send student reminder for review {ReviewId}.", review.ReviewId);
            }
        }
    }
}
