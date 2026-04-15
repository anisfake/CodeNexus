using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Events;
using CodeNexus.Application.Features.Notifications.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathShares.Notifications;

public class LearningPathDraftVersionUpdatedCreateNotificationsHandler
    : INotificationHandler<LearningPathDraftVersionUpdatedEvent>
{
    private readonly IApplicationDbContext _context;
    private readonly INotificationRealtimeNotifier _notificationRealtimeNotifier;

    public LearningPathDraftVersionUpdatedCreateNotificationsHandler(
        IApplicationDbContext context,
        INotificationRealtimeNotifier notificationRealtimeNotifier)
    {
        _context = context;
        _notificationRealtimeNotifier = notificationRealtimeNotifier;
    }

    public async Task Handle(LearningPathDraftVersionUpdatedEvent notification, CancellationToken cancellationToken)
    {
        var subscribedShares = await _context.LearningPathShares
            .Where(s => s.PathId == notification.PathId
                        && s.Status == LearningPathShareStatus.Accepted
                        && s.IsTrackingEnabled)
            .ToListAsync(cancellationToken);

        var sharesToNotify = subscribedShares
            .Where(s => s.AcceptedPathId.HasValue
                        && (s.SourceVersionAtAccept ?? 1.0m) < notification.CurrentVersion
                        && (!s.IgnoredSourceVersion.HasValue || s.IgnoredSourceVersion.Value < notification.CurrentVersion)
                        && (!s.LastNotifiedSourceVersion.HasValue || s.LastNotifiedSourceVersion.Value < notification.CurrentVersion))
            .ToList();

        if (sharesToNotify.Count == 0)
        {
            return;
        }

        var sourceLearningPathTitle = await _context.LearningPaths
            .AsNoTracking()
            .Where(lp => lp.PathId == notification.PathId)
            .Select(lp => lp.Title)
            .FirstOrDefaultAsync(cancellationToken);

        var notificationTitleSnapshot = string.IsNullOrWhiteSpace(sourceLearningPathTitle)
            ? $"Learning Path - ver {notification.CurrentVersion}"
            : sourceLearningPathTitle.Trim();

        var notifications = sharesToNotify
            .GroupBy(s => s.StudentId)
            .Select(g =>
            {
                var representativeShare = g.OrderByDescending(s => s.RespondedAt).First();
                return new Notification
                {
                    NotificationId = NewId.NextGuid(),
                    UserId = representativeShare.StudentId,
                    // Return i18n keys so FE can fully control locale switch.
                    Title = "notification.shareVersionUpdated.title",
                    Message = "notification.shareVersionUpdated.message",
                    Type = NotificationType.ShareVersionUpdated,
                    Severity = "Info",
                    Channels = "Web,Main",
                    TargetType = "learningPathShareUpdate",
                    TargetId = representativeShare.ShareId,
                    TargetUrl = $"/learning-path-shares/{representativeShare.ShareId}/updates",
                    Route = "/learningpath-shares/:shareId/updates",
                    LearningPathId = representativeShare.AcceptedPathId,
                    NotifiedPathTitle = notificationTitleSnapshot,
                    NotifiedSourceVersion = notification.CurrentVersion,
                    NotifiedMentorUserName = notification.MentorUserName,
                    IsRead = false,
                    CreatedAt = notification.OccurredAt
                };
            })
            .ToList();

        foreach (var share in sharesToNotify)
        {
            share.LastNotifiedSourceVersion = notification.CurrentVersion;
        }

        _context.Notifications.AddRange(notifications);
        await _context.SaveChangesAsync(cancellationToken);

        await _notificationRealtimeNotifier.NotifyCreatedAsync(
            notifications.Select(NotificationDtoMapper.ToDto).ToList(),
            cancellationToken);
    }
}
