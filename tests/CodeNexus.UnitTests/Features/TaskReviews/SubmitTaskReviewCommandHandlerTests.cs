using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Notifications.DTOs;
using CodeNexus.Application.Features.TaskReviews.Commands.SubmitTaskReview;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;

namespace CodeNexus.UnitTests.Features.TaskReviews;

public class SubmitTaskReviewCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<INotificationRealtimeNotifier> _mockNotifier;
    private readonly SubmitTaskReviewCommandHandler _handler;

    public SubmitTaskReviewCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockNotifier = new Mock<INotificationRealtimeNotifier>();
        _handler = new SubmitTaskReviewCommandHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object,
            _mockNotifier.Object);
    }

    private TaskReview CreatePendingReview(Guid reviewId, Guid sessionId, Guid taskId, Guid studentId, Guid mentorId)
    {
        var session = new FocusSession
        {
            SessionId = sessionId,
            TaskId = taskId,
            SubmittedCode = "x = 1",
            Task = new Domain.Entities.Tasks { TaskId = taskId, Title = "Test Task" }
        };

        return new TaskReview
        {
            ReviewId = reviewId,
            SessionId = sessionId,
            Session = session,
            TaskId = taskId,
            StudentId = studentId,
            MentorId = mentorId,
            Status = TaskReviewStatus.Pending,
            RequestedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task Handle_ValidSubmission_ReturnsSuccess()
    {
        // Arrange
        var mentorId = NewId.NextGuid();
        var studentId = NewId.NextGuid();
        var reviewId = NewId.NextGuid();
        var sessionId = NewId.NextGuid();
        var taskId = NewId.NextGuid();

        var review = CreatePendingReview(reviewId, sessionId, taskId, studentId, mentorId);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);

        var reviewsDbSet = new List<TaskReview> { review }.BuildMockDbSet();
        _mockContext.Setup(x => x.TaskReviews).Returns(reviewsDbSet.Object);

        var notificationsDbSet = new List<Notification>().BuildMockDbSet();
        notificationsDbSet.Setup(x => x.Add(It.IsAny<Notification>()));
        _mockContext.Setup(x => x.Notifications).Returns(notificationsDbSet.Object);

        _mockContext.Setup(x => x.SaveChangesAsync(CancellationToken.None)).ReturnsAsync(1);
        _mockNotifier.Setup(x => x.NotifyCreatedAsync(It.IsAny<IReadOnlyCollection<NotificationDto>>(), CancellationToken.None))
                     .Returns(Task.CompletedTask);

        var command = new SubmitTaskReviewCommand(reviewId, 85, "Great work!", "Try using list comprehensions.");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        review.Status.Should().Be(TaskReviewStatus.Reviewed);
        review.Score.Should().Be(85);
        review.Feedback.Should().Be("Great work!");
        review.Suggestions.Should().Be("Try using list comprehensions.");
        review.ReviewedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ReviewNotFound_ReturnsFailure()
    {
        // Arrange
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(NewId.NextGuid());

        var reviewsDbSet = new List<TaskReview>().BuildMockDbSet();
        _mockContext.Setup(x => x.TaskReviews).Returns(reviewsDbSet.Object);

        var command = new SubmitTaskReviewCommand(NewId.NextGuid(), 80, "Good", null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("REVIEW_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_NotAssignedMentor_ReturnsUnauthorized()
    {
        // Arrange
        var otherUserId = NewId.NextGuid();
        var mentorId = NewId.NextGuid();
        var reviewId = NewId.NextGuid();

        var review = CreatePendingReview(reviewId, NewId.NextGuid(), NewId.NextGuid(), NewId.NextGuid(), mentorId);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(otherUserId);

        var reviewsDbSet = new List<TaskReview> { review }.BuildMockDbSet();
        _mockContext.Setup(x => x.TaskReviews).Returns(reviewsDbSet.Object);

        var command = new SubmitTaskReviewCommand(reviewId, 90, "Nice", null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task Handle_AlreadyReviewed_ReturnsFailure()
    {
        // Arrange
        var mentorId = NewId.NextGuid();
        var reviewId = NewId.NextGuid();

        var review = CreatePendingReview(reviewId, NewId.NextGuid(), NewId.NextGuid(), NewId.NextGuid(), mentorId);
        review.Status = TaskReviewStatus.Reviewed;

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);

        var reviewsDbSet = new List<TaskReview> { review }.BuildMockDbSet();
        _mockContext.Setup(x => x.TaskReviews).Returns(reviewsDbSet.Object);

        var command = new SubmitTaskReviewCommand(reviewId, 75, "Well done", null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("REVIEW_ALREADY_SUBMITTED");
    }
}
