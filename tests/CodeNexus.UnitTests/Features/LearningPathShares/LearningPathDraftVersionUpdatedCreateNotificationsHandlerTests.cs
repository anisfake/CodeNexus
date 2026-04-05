using CodeNexus.Application.Common.Events;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPathShares.Notifications;
using CodeNexus.Application.Features.Notifications.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;

namespace CodeNexus.UnitTests.Features.LearningPathShares;

public class LearningPathDraftVersionUpdatedCreateNotificationsHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<INotificationRealtimeNotifier> _mockRealtimeNotifier;
    private readonly LearningPathDraftVersionUpdatedCreateNotificationsHandler _handler;

    public LearningPathDraftVersionUpdatedCreateNotificationsHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockRealtimeNotifier = new Mock<INotificationRealtimeNotifier>();

        _mockRealtimeNotifier
            .Setup(x => x.NotifyCreatedAsync(It.IsAny<IReadOnlyCollection<NotificationDto>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new LearningPathDraftVersionUpdatedCreateNotificationsHandler(
            _mockContext.Object,
            _mockRealtimeNotifier.Object);
    }

    [Fact]
    public async Task Handle_EligibleAcceptedShares_CreatesAndBroadcastsNotifications()
    {
        var pathId = NewId.NextGuid();
        var occurredAt = new DateTime(2026, 04, 05, 12, 0, 0, DateTimeKind.Utc);

        var shouldNotify = new LearningPathShare
        {
            ShareId = NewId.NextGuid(),
            PathId = pathId,
            MentorId = NewId.NextGuid(),
            StudentId = NewId.NextGuid(),
            AcceptedPathId = NewId.NextGuid(),
            Status = LearningPathShareStatus.Accepted,
            SourceVersionAtAccept = 1,
            IsTrackingEnabled = true,
            SentAt = occurredAt.AddHours(-10)
        };

        var ignoredVersion = new LearningPathShare
        {
            ShareId = NewId.NextGuid(),
            PathId = pathId,
            MentorId = NewId.NextGuid(),
            StudentId = NewId.NextGuid(),
            AcceptedPathId = NewId.NextGuid(),
            Status = LearningPathShareStatus.Accepted,
            SourceVersionAtAccept = 1,
            IgnoredSourceVersion = 2,
            IsTrackingEnabled = true,
            SentAt = occurredAt.AddHours(-8)
        };

        var trackingDisabled = new LearningPathShare
        {
            ShareId = NewId.NextGuid(),
            PathId = pathId,
            MentorId = NewId.NextGuid(),
            StudentId = NewId.NextGuid(),
            AcceptedPathId = NewId.NextGuid(),
            Status = LearningPathShareStatus.Accepted,
            SourceVersionAtAccept = 1,
            IsTrackingEnabled = false,
            SentAt = occurredAt.AddHours(-7)
        };

        var alreadyNotified = new LearningPathShare
        {
            ShareId = NewId.NextGuid(),
            PathId = pathId,
            MentorId = NewId.NextGuid(),
            StudentId = NewId.NextGuid(),
            AcceptedPathId = NewId.NextGuid(),
            Status = LearningPathShareStatus.Accepted,
            SourceVersionAtAccept = 1,
            LastNotifiedSourceVersion = 2,
            IsTrackingEnabled = true,
            SentAt = occurredAt.AddHours(-6)
        };

        var notifications = new List<Notification>();
        var notificationsDbSet = notifications.BuildMockDbSet();
        notificationsDbSet
            .Setup(x => x.AddRange(It.IsAny<IEnumerable<Notification>>()))
            .Callback<IEnumerable<Notification>>(items => notifications.AddRange(items));

        _mockContext
            .Setup(x => x.LearningPathShares)
            .Returns(new[] { shouldNotify, ignoredVersion, trackingDisabled, alreadyNotified }.BuildMockDbSet().Object);
        _mockContext
            .Setup(x => x.Notifications)
            .Returns(notificationsDbSet.Object);
        _mockContext
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var notification = new LearningPathDraftVersionUpdatedEvent(
            pathId,
            NewId.NextGuid(),
            "mentor_a",
            2,
            occurredAt);

        await _handler.Handle(notification, CancellationToken.None);

        notifications.Should().HaveCount(1);
        notifications[0].UserId.Should().Be(shouldNotify.StudentId);
        notifications[0].Type.Should().Be(NotificationType.ShareVersionUpdated);
        notifications[0].Title.Should().Be("notification.shareVersionUpdated.title");
        notifications[0].Message.Should().Be("notification.shareVersionUpdated.message");
        notifications[0].TargetId.Should().Be(shouldNotify.ShareId);
        notifications[0].LearningPathId.Should().Be(shouldNotify.AcceptedPathId);

        shouldNotify.LastNotifiedSourceVersion.Should().Be(2);
        ignoredVersion.LastNotifiedSourceVersion.Should().BeNull();
        alreadyNotified.LastNotifiedSourceVersion.Should().Be(2);

        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockRealtimeNotifier.Verify(
            x => x.NotifyCreatedAsync(
                It.Is<IReadOnlyCollection<NotificationDto>>(items => items.Count == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NoEligibleShares_DoesNotPersistOrBroadcast()
    {
        var pathId = NewId.NextGuid();
        var occurredAt = new DateTime(2026, 04, 05, 13, 0, 0, DateTimeKind.Utc);

        var ignoredVersion = new LearningPathShare
        {
            ShareId = NewId.NextGuid(),
            PathId = pathId,
            MentorId = NewId.NextGuid(),
            StudentId = NewId.NextGuid(),
            AcceptedPathId = NewId.NextGuid(),
            Status = LearningPathShareStatus.Accepted,
            SourceVersionAtAccept = 1,
            IgnoredSourceVersion = 2,
            IsTrackingEnabled = true,
            SentAt = occurredAt.AddHours(-8)
        };

        var notificationsDbSet = new List<Notification>().BuildMockDbSet();

        _mockContext
            .Setup(x => x.LearningPathShares)
            .Returns(new[] { ignoredVersion }.BuildMockDbSet().Object);
        _mockContext
            .Setup(x => x.Notifications)
            .Returns(notificationsDbSet.Object);

        var notification = new LearningPathDraftVersionUpdatedEvent(
            pathId,
            NewId.NextGuid(),
            "mentor_a",
            2,
            occurredAt);

        await _handler.Handle(notification, CancellationToken.None);

        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockRealtimeNotifier.Verify(
            x => x.NotifyCreatedAsync(It.IsAny<IReadOnlyCollection<NotificationDto>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
