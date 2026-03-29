using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Notifications.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CodeNexus.Application.Features.Notifications.Commands.CreateOverdueNotifications;

public class CreateOverdueNotificationsCommandHandler
    : IRequestHandler<CreateOverdueNotificationsCommand, Result<CreateOverdueNotificationsResultDto>>
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromHours(24);

    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly INotificationRealtimeNotifier _notificationRealtimeNotifier;
    private readonly ILogger<CreateOverdueNotificationsCommandHandler> _logger;

    public CreateOverdueNotificationsCommandHandler(
        IApplicationDbContext context,
        IEmailService emailService,
        INotificationRealtimeNotifier notificationRealtimeNotifier,
        ILogger<CreateOverdueNotificationsCommandHandler> logger)
    {
        _context = context;
        _emailService = emailService;
        _notificationRealtimeNotifier = notificationRealtimeNotifier;
        _logger = logger;
    }

    public async Task<Result<CreateOverdueNotificationsResultDto>> Handle(CreateOverdueNotificationsCommand request, CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;

        var candidates = new List<OverdueNotificationCandidate>();
        candidates.AddRange(await DetectOverdueTasksAsync(nowUtc, cancellationToken));
        candidates.AddRange(await DetectOverdueLearningPathsAsync(nowUtc, cancellationToken));
        candidates.AddRange(await DetectOverdueChaptersAsync(nowUtc, cancellationToken));
        candidates.AddRange(await DetectOverdueLessonsAsync(nowUtc, cancellationToken));
        candidates.AddRange(await DetectPlanExpiringSoonAsync(nowUtc, cancellationToken));
        candidates.AddRange(await DetectPlanExpiredAsync(nowUtc, cancellationToken));

        if (candidates.Count == 0)
        {
            return Result<CreateOverdueNotificationsResultDto>.Success(new CreateOverdueNotificationsResultDto(0, 0, 0, 0, 0, 0, 0, 0));
        }

        var dedupSinceUtc = nowUtc.Subtract(Cooldown);

        var existingKeys = await _context.Notifications
            .AsNoTracking()
            .Where(n => n.Type.HasValue && n.CreatedAt >= dedupSinceUtc)
            .Select(n => new DedupKey(n.UserId, n.Type!.Value, n.Title))
            .ToListAsync(cancellationToken);

        var dedupSet = existingKeys.ToHashSet();
        var toCreate = new List<OverdueNotificationCandidate>();

        foreach (var candidate in candidates)
        {
            if (!candidate.Channels.Contains(NotificationChannel.Web))
            {
                continue;
            }

            var key = new DedupKey(candidate.UserId, candidate.Type, candidate.Title);
            if (!dedupSet.Add(key))
            {
                continue;
            }

            toCreate.Add(candidate);
        }

        if (toCreate.Count > 0)
        {
            var notifications = toCreate.Select(candidate => new Notification
            {
                NotificationId = NewId.NextGuid(),
                UserId = candidate.UserId,
                Title = candidate.Title,
                Message = candidate.Message,
                Type = candidate.Type,
                IsRead = false,
                CreatedAt = nowUtc
            }).ToList();

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync(cancellationToken);

            await _notificationRealtimeNotifier.NotifyCreatedAsync(
                notifications
                    .Select(n => new NotificationRealtimeDto(
                        n.NotificationId,
                        n.UserId,
                        n.Title,
                        n.Message,
                        n.Type,
                        n.CreatedAt))
                    .ToList(),
                cancellationToken);

            await SendEmailNotificationsAsync(toCreate, cancellationToken);
        }

        var response = new CreateOverdueNotificationsResultDto(
            candidates.Count,
            toCreate.Count,
            toCreate.Count(x => x.Type == NotificationType.TaskOverdue),
            toCreate.Count(x => x.Type == NotificationType.LearningPathOverdue),
            toCreate.Count(x => x.Type == NotificationType.ChapterOverdue),
            toCreate.Count(x => x.Type == NotificationType.LessonOverdue),
            toCreate.Count(x => x.Type == NotificationType.PlanExpiringSoon),
            toCreate.Count(x => x.Type == NotificationType.PlanExpired));

        return Result<CreateOverdueNotificationsResultDto>.Success(response);
    }

    private async Task<List<OverdueNotificationCandidate>> DetectOverdueTasksAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var rows = await _context.Tasks
            .AsNoTracking()
            .Where(t => t.DueDate.HasValue
                        && t.DueDate.Value < nowUtc
                        && t.Status != TaskStatus_.Completed)
            .Select(t => new
            {
                t.TaskId,
                t.Title,
                t.DueDate,
                t.PathId,
                PathTitle = t.LearningPath.Title,
                UserId = t.LearningPath.UserId
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x => new OverdueNotificationCandidate(
                x.UserId,
                NotificationType.TaskOverdue,
                $"Task quá hạn: {x.Title}",
                BuildTaskOverdueMessage(x.PathTitle, x.Title, x.DueDate),
                ResolveChannels(OverdueNotificationKind.TaskOverdue)))
            .ToList();
    }

    private async Task<List<OverdueNotificationCandidate>> DetectOverdueLearningPathsAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var rows = await _context.LearningPaths
            .AsNoTracking()
            .Where(lp => lp.EndDate.HasValue
                         && lp.EndDate.Value < nowUtc
                         && lp.Status != LearningPathStatus.Completed.ToString()
                         && lp.Status != LearningPathStatus.Cancelled.ToString()
                         && lp.Status != LearningPathStatus.Draft.ToString())
            .Select(lp => new
            {
                lp.PathId,
                lp.UserId,
                lp.Title,
                lp.EndDate
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x => new OverdueNotificationCandidate(
                x.UserId,
                NotificationType.LearningPathOverdue,
                $"Lộ trình quá hạn: {x.Title}",
                BuildLearningPathOverdueMessage(x.Title, x.EndDate),
                ResolveChannels(OverdueNotificationKind.LearningPathOverdue)))
            .ToList();
    }

    private async Task<List<OverdueNotificationCandidate>> DetectOverdueChaptersAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var rows = await _context.Chapters
            .AsNoTracking()
            .Where(c => c.EndDate.HasValue
                        && c.EndDate.Value < nowUtc
                        && !c.IsCompleted
                        && !c.IsDeleted)
            .Select(c => new
            {
                c.ChapterId,
                c.Title,
                c.EndDate,
                UserId = c.LearningPath.UserId,
                PathTitle = c.LearningPath.Title
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x => new OverdueNotificationCandidate(
                x.UserId,
                NotificationType.ChapterOverdue,
                $"Chương quá hạn: {x.Title}",
                BuildChapterOverdueMessage(x.PathTitle, x.Title, x.EndDate),
                ResolveChannels(OverdueNotificationKind.ChapterOverdue)))
            .ToList();
    }

    private async Task<List<OverdueNotificationCandidate>> DetectOverdueLessonsAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var rows = await _context.Lessons
            .AsNoTracking()
            .Where(l => !l.IsDeleted && l.LessonDay < nowUtc)
            .Select(l => new
            {
                l.LessonId,
                l.Title,
                l.LessonDay,
                UserId = l.Chapter.LearningPath.UserId,
                ChapterTitle = l.Chapter.Title,
                PathTitle = l.Chapter.LearningPath.Title
            })
            .Where(x => !_context.LearnProgresses.Any(p => p.LessonId == x.LessonId && p.UserId == x.UserId))
            .ToListAsync(cancellationToken);

        return rows.Select(x => new OverdueNotificationCandidate(
                x.UserId,
                NotificationType.LessonOverdue,
                $"Bài học quá hạn: {x.Title}",
                BuildLessonOverdueMessage(x.PathTitle, x.ChapterTitle, x.Title, x.LessonDay),
                ResolveChannels(OverdueNotificationKind.LessonOverdue)))
            .ToList();
    }

    private async Task<List<OverdueNotificationCandidate>> DetectPlanExpiringSoonAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var thresholdUtc = nowUtc.AddDays(3);

        var rows = await _context.Users
            .AsNoTracking()
            .Where(u => u.PlanExpiresAt.HasValue
                        && u.PlanExpiresAt.Value >= nowUtc
                        && u.PlanExpiresAt.Value <= thresholdUtc)
            .Select(u => new
            {
                u.UserId,
                u.PlanExpiresAt
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x => new OverdueNotificationCandidate(
                x.UserId,
                NotificationType.PlanExpiringSoon,
                "Gói dịch vụ sắp hết hạn",
                BuildPlanExpiringSoonMessage(x.PlanExpiresAt),
                ResolveChannels(OverdueNotificationKind.PlanExpiringSoon)))
            .ToList();
    }

    private async Task<List<OverdueNotificationCandidate>> DetectPlanExpiredAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var rows = await _context.Users
            .AsNoTracking()
            .Where(u => u.PlanExpiresAt.HasValue && u.PlanExpiresAt.Value < nowUtc)
            .Select(u => new
            {
                u.UserId,
                u.PlanExpiresAt
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x => new OverdueNotificationCandidate(
                x.UserId,
                NotificationType.PlanExpired,
                "Gói dịch vụ đã hết hạn",
                BuildPlanExpiredMessage(x.PlanExpiresAt),
                ResolveChannels(OverdueNotificationKind.PlanExpired)))
            .ToList();
    }

    private static NotificationChannel[] ResolveChannels(OverdueNotificationKind kind)
    {
        return kind switch
        {
            OverdueNotificationKind.TaskOverdue => [NotificationChannel.Web, NotificationChannel.Main],
            OverdueNotificationKind.LearningPathOverdue => [NotificationChannel.Web, NotificationChannel.Main, NotificationChannel.Email],
            OverdueNotificationKind.ChapterOverdue => [NotificationChannel.Web, NotificationChannel.Main],
            OverdueNotificationKind.LessonOverdue => [NotificationChannel.Web],
            OverdueNotificationKind.PlanExpiringSoon => [NotificationChannel.Web, NotificationChannel.Main, NotificationChannel.Email],
            OverdueNotificationKind.PlanExpired => [NotificationChannel.Web, NotificationChannel.Main, NotificationChannel.Email],
            _ => [NotificationChannel.Web]
        };
    }

    private enum OverdueNotificationKind
    {
        TaskOverdue,
        LearningPathOverdue,
        ChapterOverdue,
        LessonOverdue,
        PlanExpiringSoon,
        PlanExpired
    }

    private enum NotificationChannel
    {
        Web,
        Main,
        Email
    }

	private static string BuildTaskOverdueMessage(string pathTitle, string taskTitle, DateTime? dueDate)
		=> $"Task '{taskTitle}' in learning path '{pathTitle}' has been overdue since {dueDate:dd/MM/yyyy HH:mm} UTC.\n"
		 + "You should complete this task as soon as possible to avoid falling behind on your learning progress.";

	private static string BuildChapterOverdueMessage(string pathTitle, string chapterTitle, DateTime? endDate)
		=> $"Chapter '{chapterTitle}' in learning path '{pathTitle}' has been overdue since {endDate:dd/MM/yyyy HH:mm} UTC.\n"
		 + "Continue studying this chapter to stay on track with your plan.";

	private static string BuildLessonOverdueMessage(string pathTitle, string chapterTitle, string lessonTitle, DateTime lessonDay)
		=> $"Lesson '{lessonTitle}' (chapter '{chapterTitle}', path '{pathTitle}') has been overdue since {lessonDay:dd/MM/yyyy HH:mm} UTC.\n"
		 + "Complete this lesson to unlock the next milestones.";

	private static string BuildLearningPathOverdueMessage(string learningPathTitle, DateTime? endDate)
		=> $"Learning path '{learningPathTitle}' has been overdue since {endDate:dd/MM/yyyy HH:mm} UTC.\n"
		 + "Impact: your learning progress may be disrupted and related tasks/chapters will continue to accumulate.\n"
		 + "Recommendation: open CodeNexus to review your timeline and complete any outstanding items.";

	private static string BuildPlanExpiringSoonMessage(DateTime? planExpiresAt)
		=> $"Your current subscription plan will expire on {planExpiresAt:dd/MM/yyyy HH:mm} UTC.\n"
		 + "Impact after expiration: some advanced features may become restricted.\n"
		 + "Recommendation: renew or upgrade your plan before the expiration date to avoid any interruption.";

	private static string BuildPlanExpiredMessage(DateTime? planExpiresAt)
		=> $"Your subscription plan has expired as of {planExpiresAt:dd/MM/yyyy HH:mm} UTC.\n"
		 + "Current impact: features included in your paid plan may no longer be available.\n"
		 + "Recommendation: visit the Billing/Subscription section to renew immediately.";
	private async Task SendEmailNotificationsAsync(List<OverdueNotificationCandidate> notifications, CancellationToken cancellationToken)
    {
        var emailCandidates = notifications
            .Where(x => x.Channels.Contains(NotificationChannel.Email))
            .ToList();

        if (emailCandidates.Count == 0)
        {
            return;
        }

        var userIds = emailCandidates.Select(x => x.UserId).Distinct().ToList();
        var emailByUserId = await _context.Users
            .AsNoTracking()
            .Where(x => userIds.Contains(x.UserId))
            .Select(x => new { x.UserId, x.Email })
            .ToDictionaryAsync(x => x.UserId, x => x.Email, cancellationToken);

        foreach (var notification in emailCandidates)
        {
            if (!emailByUserId.TryGetValue(notification.UserId, out var email)
                || string.IsNullOrWhiteSpace(email))
            {
                continue;
            }

            try
            {
                await _emailService.SendNotificationEmailAsync(
                    email,
                    notification.Title,
                    notification.Message,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to send overdue notification email to user {UserId}",
                    notification.UserId);
            }
        }
    }

    private sealed record OverdueNotificationCandidate(
        Guid UserId,
        NotificationType Type,
        string Title,
        string Message,
        NotificationChannel[] Channels);

    private readonly record struct DedupKey(Guid UserId, NotificationType Type, string Title);
}
