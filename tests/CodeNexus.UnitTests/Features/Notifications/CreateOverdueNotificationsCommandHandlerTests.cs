using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Notifications.Commands.CreateOverdueNotifications;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.Notifications;

public class CreateOverdueNotificationsCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<IEmailService> _mockEmailService;
    private readonly Mock<INotificationRealtimeNotifier> _mockRealtimeNotifier;
    private readonly CreateOverdueNotificationsCommandHandler _handler;

    public CreateOverdueNotificationsCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockEmailService = new Mock<IEmailService>();
        _mockRealtimeNotifier = new Mock<INotificationRealtimeNotifier>();

        _mockRealtimeNotifier
            .Setup(x => x.NotifyCreatedAsync(It.IsAny<IReadOnlyCollection<CodeNexus.Application.Features.Notifications.DTOs.NotificationDto>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockEmailService
            .Setup(x => x.SendNotificationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var logger = new Mock<Microsoft.Extensions.Logging.ILogger<CreateOverdueNotificationsCommandHandler>>();
        _handler = new CreateOverdueNotificationsCommandHandler(
            _mockContext.Object,
            _mockEmailService.Object,
            _mockRealtimeNotifier.Object,
            logger.Object);
    }

    [Fact]
    public async Task Handle_OverdueTaskExists_CreatesNotification()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var pathId = NewId.NextGuid();
        var chapterId = NewId.NextGuid();

        var task = new Domain.Entities.Tasks
        {
            TaskId = NewId.NextGuid(),
            PathId = pathId,
            ChapterId = chapterId,
            Title = "Nộp bài project",
            DueDate = DateTime.UtcNow.AddHours(-6),
            Status = TaskStatus_.Pending,
            LearningPath = new LearningPath
            {
                PathId = pathId,
                UserId = userId,
                Title = "Backend .NET"
            },
            Chapter = new Chapter
            {
                ChapterId = chapterId,
                PathId = pathId,
                Title = "CQRS"
            }
        };

        _mockContext.Setup(x => x.Tasks).Returns(new[] { task }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Chapters).Returns(new List<Chapter>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Lessons).Returns(new List<Lesson>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearnProgresses).Returns(new List<LearnProgress>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Users).Returns(new List<User> { new() { UserId = userId } }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Notifications).Returns(new List<Notification>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(new CreateOverdueNotificationsCommand(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.CreatedCount.Should().Be(1);
        result.Value.TaskOverdueCount.Should().Be(1);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateNotificationInCooldown_SkipsCreation()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var pathId = NewId.NextGuid();
        var chapterId = NewId.NextGuid();

        var task = new Domain.Entities.Tasks
        {
            TaskId = NewId.NextGuid(),
            PathId = pathId,
            ChapterId = chapterId,
            Title = "Nộp bài project",
            DueDate = DateTime.UtcNow.AddHours(-2),
            Status = TaskStatus_.Pending,
            LearningPath = new LearningPath
            {
                PathId = pathId,
                UserId = userId,
                Title = "Backend .NET"
            },
            Chapter = new Chapter
            {
                ChapterId = chapterId,
                PathId = pathId,
                Title = "CQRS"
            }
        };

        var existing = new Notification
        {
            NotificationId = NewId.NextGuid(),
            UserId = userId,
            Type = NotificationType.TaskOverdue,
            Title = "Task quá hạn: Nộp bài project",
            TargetId = task.TaskId,
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };

        _mockContext.Setup(x => x.Tasks).Returns(new[] { task }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Chapters).Returns(new List<Chapter>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Lessons).Returns(new List<Lesson>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearnProgresses).Returns(new List<LearnProgress>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Users).Returns(new List<User> { new() { UserId = userId } }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Notifications).Returns(new[] { existing }.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(new CreateOverdueNotificationsCommand(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.CreatedCount.Should().Be(0);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserNotInEligibleList_DoesNotCreateNotification()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var anotherUserId = NewId.NextGuid();
        var pathId = NewId.NextGuid();
        var chapterId = NewId.NextGuid();

        var task = new Domain.Entities.Tasks
        {
            TaskId = NewId.NextGuid(),
            PathId = pathId,
            ChapterId = chapterId,
            Title = "Nộp bài project",
            DueDate = DateTime.UtcNow.AddHours(-6),
            Status = TaskStatus_.Pending,
            LearningPath = new LearningPath
            {
                PathId = pathId,
                UserId = userId,
                Title = "Backend .NET"
            },
            Chapter = new Chapter
            {
                ChapterId = chapterId,
                PathId = pathId,
                Title = "CQRS"
            }
        };

        _mockContext.Setup(x => x.Tasks).Returns(new[] { task }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Chapters).Returns(new List<Chapter>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Lessons).Returns(new List<Lesson>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearnProgresses).Returns(new List<LearnProgress>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Users).Returns(new List<User> { new() { UserId = userId } }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Notifications).Returns(new List<Notification>().BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(
            new CreateOverdueNotificationsCommand(new[] { anotherUserId }),
            CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.CreatedCount.Should().Be(0);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DuplicateFromPreviousDay_CreatesNotificationAgain()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var pathId = NewId.NextGuid();
        var chapterId = NewId.NextGuid();

        var task = new Domain.Entities.Tasks
        {
            TaskId = NewId.NextGuid(),
            PathId = pathId,
            ChapterId = chapterId,
            Title = "Nộp bài project",
            DueDate = DateTime.UtcNow.AddHours(-2),
            Status = TaskStatus_.Pending,
            LearningPath = new LearningPath
            {
                PathId = pathId,
                UserId = userId,
                Title = "Backend .NET"
            },
            Chapter = new Chapter
            {
                ChapterId = chapterId,
                PathId = pathId,
                Title = "CQRS"
            }
        };

        var oldNotification = new Notification
        {
            NotificationId = NewId.NextGuid(),
            UserId = userId,
            Type = NotificationType.TaskOverdue,
            Title = "Task quá hạn: Nộp bài project",
            TargetId = task.TaskId,
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };

        _mockContext.Setup(x => x.Tasks).Returns(new[] { task }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Chapters).Returns(new List<Chapter>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Lessons).Returns(new List<Lesson>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearnProgresses).Returns(new List<LearnProgress>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Users).Returns(new List<User> { new() { UserId = userId } }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Notifications).Returns(new[] { oldNotification }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(new CreateOverdueNotificationsCommand(new[] { userId }), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.CreatedCount.Should().Be(1);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
