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
    private static readonly TimeSpan ReminderThreshold = TimeSpan.FromDays(2);

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

        var threshold = DateTime.UtcNow.Subtract(ReminderThreshold);

        var overdueReviews = await context.LearningPathMentorReviews
            .Include(r => r.Student)
            .Include(r => r.Mentor)
            .Include(r => r.LearningPath)
            .Where(r =>
                r.DecisionStatus == LearningPathMentorReviewDecisionStatus.WaitingStudentResponse
                && r.MentorRespondedAt.HasValue
                && r.MentorRespondedAt.Value <= threshold)
            .ToListAsync(cancellationToken);

        foreach (var review in overdueReviews)
        {
            try
            {
                var studentName = review.Student.FirstName ?? review.Student.Username;
                var mentorName = review.Mentor.FirstName ?? review.Mentor.Username;
                var pathTitle = review.LearningPath.Title;

                // Send email
                var subject = "Nhắc nhở: Lộ trình học của bạn đang chờ phản hồi";
                var message = $"Xin chào {studentName},\n\n" +
                              $"Mentor {mentorName} đã hoàn thành review lộ trình \"{pathTitle}\" của bạn và đang chờ bạn phản hồi.\n\n" +
                              $"Vui lòng đăng nhập vào hệ thống để xem nhận xét và chấp nhận hoặc từ chối đề xuất của mentor.\n\n" +
                              $"Trân trọng,\nCodeNexus";

                await emailService.SendNotificationEmailAsync(review.Student.Email, subject, message, cancellationToken);

                // Create in-app notification
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
                    "Sent mentor review reminder to student {StudentId} for review {ReviewId}.",
                    review.StudentId, review.ReviewId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to send reminder for review {ReviewId}.", review.ReviewId);
            }
        }
    }
}
