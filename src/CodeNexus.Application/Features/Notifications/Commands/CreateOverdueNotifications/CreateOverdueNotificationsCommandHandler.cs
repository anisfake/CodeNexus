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
        var eligibleUserIds = request.EligibleUserIds?.Distinct().ToList();
        if (eligibleUserIds is { Count: 0 })
        {
            return Result<CreateOverdueNotificationsResultDto>.Success(new CreateOverdueNotificationsResultDto(0, 0, 0, 0, 0, 0));
        }

        var candidates = new List<OverdueNotificationCandidate>();
        candidates.AddRange(await DetectOverdueTasksAsync(nowUtc, eligibleUserIds, cancellationToken));
        candidates.AddRange(await DetectOverdueLearningPathsAsync(nowUtc, eligibleUserIds, cancellationToken));
        candidates.AddRange(await DetectOverdueChaptersAsync(nowUtc, eligibleUserIds, cancellationToken));
        candidates.AddRange(await DetectOverdueLessonsAsync(nowUtc, eligibleUserIds, cancellationToken));

        if (candidates.Count == 0)
        {
            return Result<CreateOverdueNotificationsResultDto>.Success(new CreateOverdueNotificationsResultDto(0, 0, 0, 0, 0, 0));
        }

        var (vnDayStartUtc, vnDayEndUtc) = GetVietnamDayWindowUtc(nowUtc);

        var existingKeys = await _context.Notifications
            .AsNoTracking()
            .Where(n => n.Type.HasValue && n.CreatedAt >= vnDayStartUtc && n.CreatedAt < vnDayEndUtc)
            .Select(n => new DedupKey(n.UserId, n.Type!.Value, n.TargetId, n.Title))
            .ToListAsync(cancellationToken);

        var dedupSet = existingKeys.ToHashSet();
        var toCreate = new List<OverdueNotificationCandidate>();

        foreach (var candidate in candidates)
        {
            if (!candidate.Channels.Contains(NotificationChannel.Web))
            {
                continue;
            }

            var key = new DedupKey(candidate.UserId, candidate.Type, candidate.Action.TargetId, candidate.Title);
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
                Severity = candidate.Severity,
                Channels = string.Join(',', candidate.Channels),
                TargetType = candidate.Action.TargetType,
                TargetId = candidate.Action.TargetId,
                TargetUrl = candidate.Action.TargetUrl,
                Route = candidate.Action.Route,
                TaskId = candidate.Action.TaskId,
                ChapterId = candidate.Action.ChapterId,
                LessonId = candidate.Action.LessonId,
                LearningPathId = candidate.Action.LearningPathId,
                IsRead = false,
                CreatedAt = nowUtc
            }).ToList();

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync(cancellationToken);

            await _notificationRealtimeNotifier.NotifyCreatedAsync(
                notifications
                    .Select(NotificationDtoMapper.ToDto)
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
            toCreate.Count(x => x.Type == NotificationType.LessonOverdue));

        return Result<CreateOverdueNotificationsResultDto>.Success(response);
    }

    private async Task<List<OverdueNotificationCandidate>> DetectOverdueTasksAsync(DateTime nowUtc, List<Guid>? eligibleUserIds, CancellationToken cancellationToken)
    {
        var query = _context.Tasks
            .AsNoTracking()
            .Where(t => t.DueDate.HasValue
                        && t.DueDate.Value < nowUtc
                        && t.Status != TaskStatus_.Completed);

        if (eligibleUserIds is { Count: > 0 })
        {
            query = query.Where(t => eligibleUserIds.Contains(t.LearningPath.UserId));
        }

        var rows = await query
            .Select(t => new
            {
                t.TaskId,
                t.Title,
                t.DueDate,
                t.PathId,
                t.ChapterId,
                PathTitle = t.LearningPath.Title,
                UserId = t.LearningPath.UserId
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x => new OverdueNotificationCandidate(
                x.UserId,
                NotificationType.TaskOverdue,
                $"Task quá hạn: {x.Title}",
                BuildTaskOverdueMessage(x.PathTitle, x.Title, x.DueDate),
                ResolveSeverity(OverdueNotificationKind.TaskOverdue),
                ResolveChannels(OverdueNotificationKind.TaskOverdue),
                new NotificationActionDto(
                    "task",
                    x.TaskId,
                    $"/learning-paths/{x.PathId}/chapters/{x.ChapterId}/tasks/{x.TaskId}",
                    "/tasks/:taskId",
                    x.TaskId,
                    x.ChapterId,
                    null,
                    x.PathId)))
            .ToList();
    }

    private async Task<List<OverdueNotificationCandidate>> DetectOverdueLearningPathsAsync(DateTime nowUtc, List<Guid>? eligibleUserIds, CancellationToken cancellationToken)
    {
        var query = _context.LearningPaths
            .AsNoTracking()
            .Where(lp => lp.EndDate.HasValue
                         && lp.EndDate.Value < nowUtc
                         && lp.Status != LearningPathStatus.Completed.ToString()
                         && lp.Status != LearningPathStatus.Cancelled.ToString()
                         && lp.Status != LearningPathStatus.Draft.ToString());

        if (eligibleUserIds is { Count: > 0 })
        {
            query = query.Where(lp => eligibleUserIds.Contains(lp.UserId));
        }

        var rows = await query
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
                ResolveSeverity(OverdueNotificationKind.LearningPathOverdue),
                ResolveChannels(OverdueNotificationKind.LearningPathOverdue),
                new NotificationActionDto(
                    "learningPath",
                    x.PathId,
                    $"/learning-paths/{x.PathId}",
                    "/learning-paths/:learningPathId",
                    null,
                    null,
                    null,
                    x.PathId)))
            .ToList();
    }

    private async Task<List<OverdueNotificationCandidate>> DetectOverdueChaptersAsync(DateTime nowUtc, List<Guid>? eligibleUserIds, CancellationToken cancellationToken)
    {
        var query = _context.Chapters
            .AsNoTracking()
            .Where(c => c.EndDate.HasValue
                        && c.EndDate.Value < nowUtc
                        && !c.IsCompleted
                        && !c.IsDeleted);

        if (eligibleUserIds is { Count: > 0 })
        {
            query = query.Where(c => eligibleUserIds.Contains(c.LearningPath.UserId));
        }

        var rows = await query
            .Select(c => new
            {
                c.ChapterId,
                c.Title,
                c.EndDate,
                PathId = c.LearningPath.PathId,
                UserId = c.LearningPath.UserId,
                PathTitle = c.LearningPath.Title
            })
            .ToListAsync(cancellationToken);

        return rows.Select(x => new OverdueNotificationCandidate(
                x.UserId,
                NotificationType.ChapterOverdue,
                $"Chương quá hạn: {x.Title}",
                BuildChapterOverdueMessage(x.PathTitle, x.Title, x.EndDate),
                ResolveSeverity(OverdueNotificationKind.ChapterOverdue),
                ResolveChannels(OverdueNotificationKind.ChapterOverdue),
                new NotificationActionDto(
                    "chapter",
                    x.ChapterId,
                    $"/learning-paths/{x.PathId}/chapters/{x.ChapterId}",
                    "/chapters/:chapterId",
                    null,
                    x.ChapterId,
                    null,
                    x.PathId)))
            .ToList();
    }

    private async Task<List<OverdueNotificationCandidate>> DetectOverdueLessonsAsync(DateTime nowUtc, List<Guid>? eligibleUserIds, CancellationToken cancellationToken)
    {
        var query = _context.Lessons
            .AsNoTracking()
            .Where(l => !l.IsDeleted && l.LessonDay < nowUtc);

        if (eligibleUserIds is { Count: > 0 })
        {
            query = query.Where(l => eligibleUserIds.Contains(l.Chapter.LearningPath.UserId));
        }

        var rows = await query
            .Select(l => new
            {
                l.LessonId,
                l.Title,
                l.LessonDay,
                ChapterId = l.Chapter.ChapterId,
                PathId = l.Chapter.LearningPath.PathId,
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
                ResolveSeverity(OverdueNotificationKind.LessonOverdue),
                ResolveChannels(OverdueNotificationKind.LessonOverdue),
                new NotificationActionDto(
                    "lesson",
                    x.LessonId,
                    $"/learning-paths/{x.PathId}/chapters/{x.ChapterId}/lessons/{x.LessonId}",
                    "/lessons/:lessonId",
                    null,
                    x.ChapterId,
                    x.LessonId,
                    x.PathId)))
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
            _ => [NotificationChannel.Web]
        };
    }

    private static string ResolveSeverity(OverdueNotificationKind kind)
    {
        return kind switch
        {
            OverdueNotificationKind.LearningPathOverdue => "High",
            OverdueNotificationKind.ChapterOverdue => "Medium",
            OverdueNotificationKind.TaskOverdue => "Medium",
            _ => "Low"
        };
    }

    private enum OverdueNotificationKind
    {
        TaskOverdue,
        LearningPathOverdue,
        ChapterOverdue,
        LessonOverdue
    }

    private enum NotificationChannel
    {
        Web,
        Main,
        Email
    }

    private static (DateTime StartUtc, DateTime EndUtc) GetVietnamDayWindowUtc(DateTime nowUtc)
    {
        var timezone = ResolveVietnamTimeZone();
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timezone);
        var startLocal = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, 0, 0, 0, DateTimeKind.Unspecified);
        var endLocalExclusive = startLocal.AddDays(1);

        return (
            TimeZoneInfo.ConvertTimeToUtc(startLocal, timezone),
            TimeZoneInfo.ConvertTimeToUtc(endLocalExclusive, timezone));
    }

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
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
        string Severity,
        NotificationChannel[] Channels,
        NotificationActionDto Action);

    private readonly record struct DedupKey(Guid UserId, NotificationType Type, Guid? TargetId, string Title);
}
