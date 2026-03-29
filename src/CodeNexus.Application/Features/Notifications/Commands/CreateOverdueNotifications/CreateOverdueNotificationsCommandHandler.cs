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
                $"Task trong lộ trình '{x.PathTitle}' đã quá hạn từ {x.DueDate:dd/MM/yyyy HH:mm} UTC.",
                ResolveChannels(OverdueNotificationKind.TaskOverdue)))
            .ToList();
    }

    private async Task<List<OverdueNotificationCandidate>> DetectOverdueLearningPathsAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var rows = await _context.LearningPaths
            .AsNoTracking()
            .Where(lp => lp.EndDate.HasValue
                         && lp.EndDate.Value < nowUtc
                         && !string.Equals(lp.Status, LearningPathStatus.Completed.ToString(), StringComparison.OrdinalIgnoreCase)
                         && !string.Equals(lp.Status, LearningPathStatus.Cancelled.ToString(), StringComparison.OrdinalIgnoreCase)
                         && !string.Equals(lp.Status, LearningPathStatus.Draft.ToString(), StringComparison.OrdinalIgnoreCase))
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
                $"Lộ trình học đã quá hạn từ {x.EndDate:dd/MM/yyyy HH:mm} UTC.",
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
                $"Chương trong lộ trình '{x.PathTitle}' đã quá hạn từ {x.EndDate:dd/MM/yyyy HH:mm} UTC.",
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
                $"Bài học trong chương '{x.ChapterTitle}' của lộ trình '{x.PathTitle}' đã trễ từ {x.LessonDay:dd/MM/yyyy HH:mm} UTC.",
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
                $"Gói hiện tại sẽ hết hạn vào {x.PlanExpiresAt:dd/MM/yyyy HH:mm} UTC.",
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
                $"Gói hiện tại đã hết hạn từ {x.PlanExpiresAt:dd/MM/yyyy HH:mm} UTC.",
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
