using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Notifications.DTOs;
using CodeNexus.Application.Features.TaskReviews.Commands.RequestTaskReview;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;

namespace CodeNexus.UnitTests.Features.TaskReviews;

public class RequestTaskReviewCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<INotificationRealtimeNotifier> _mockNotifier;
    private readonly RequestTaskReviewCommandHandler _handler;

    public RequestTaskReviewCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockNotifier = new Mock<INotificationRealtimeNotifier>();
        _handler = new RequestTaskReviewCommandHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object,
            _mockNotifier.Object);
    }

    private (Guid studentId, Guid mentorId, Guid sessionId, Guid taskId, Guid subscriptionId, Guid mentorPackageId) SetupValidData(
        out FocusSession session,
        out StudentMentorSubscription subscription,
        out MentorPackage mentorPackage)
    {
        var studentId = NewId.NextGuid();
        var mentorId = NewId.NextGuid();
        var sessionId = NewId.NextGuid();
        var taskId = NewId.NextGuid();
        var pathId = NewId.NextGuid();
        var subscriptionId = NewId.NextGuid();
        var mentorPackageId = NewId.NextGuid();

        var learningPath = new LearningPath { PathId = pathId, UserId = studentId, Title = "Test Path" };
        var chapter = new Chapter { ChapterId = NewId.NextGuid(), PathId = pathId, Title = "Chapter 1" };
        var task = new Domain.Entities.Tasks
        {
            TaskId = taskId,
            PathId = pathId,
            ChapterId = chapter.ChapterId,
            Title = "Practice Task",
            LearningPath = learningPath,
            Chapter = chapter
        };

        session = new FocusSession
        {
            SessionId = sessionId,
            TaskId = taskId,
            Task = task,
            SubmittedCode = "print('hello')",
            SessionStatus = SessionStatus.CompletedOnTime
        };

        mentorPackage = new MentorPackage
        {
            MentorPackageId = mentorPackageId,
            TaskReviewLimit = 5
        };

        subscription = new StudentMentorSubscription
        {
            SubscriptionId = subscriptionId,
            UserId = studentId,
            MentorPackageId = mentorPackageId,
            MentorPackage = mentorPackage,
            IsActive = true,
            TaskReviewLimit = 5,
            TaskReviewsUsed = 0,
            SharesFromMentorLimit = -1,
            SharesFromMentorUsed = 0,
            ValidationRequestLimit = -1,
            ValidationRequestsUsed = 0
        };

        return (studentId, mentorId, sessionId, taskId, subscriptionId, mentorPackageId);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsSuccessWithReviewId()
    {
        // Arrange
        var (studentId, mentorId, sessionId, taskId, subscriptionId, mentorPackageId) =
            SetupValidData(out var session, out var subscription, out var mentorPackage);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);

        var sessionsDbSet = new List<FocusSession> { session }.BuildMockDbSet();
        _mockContext.Setup(x => x.FocusSessions).Returns(sessionsDbSet.Object);

        var reviewsDbSet = new List<TaskReview>().BuildMockDbSet();
        reviewsDbSet.Setup(x => x.Add(It.IsAny<TaskReview>()));
        _mockContext.Setup(x => x.TaskReviews).Returns(reviewsDbSet.Object);

        var subscriptionsDbSet = new List<StudentMentorSubscription> { subscription }.BuildMockDbSet();
        _mockContext.Setup(x => x.StudentMentorSubscriptions).Returns(subscriptionsDbSet.Object);

        var mentorPackagesDbSet = new List<MentorPackage> { mentorPackage }.BuildMockDbSet();
        _mockContext.Setup(x => x.MentorPackages).Returns(mentorPackagesDbSet.Object);

        var conversationsDbSet = new List<DirectConversation>().BuildMockDbSet();
        conversationsDbSet.Setup(x => x.Add(It.IsAny<DirectConversation>()));
        _mockContext.Setup(x => x.DirectConversations).Returns(conversationsDbSet.Object);

        var messagesDbSet = new List<DirectMessage>().BuildMockDbSet();
        messagesDbSet.Setup(x => x.Add(It.IsAny<DirectMessage>()));
        _mockContext.Setup(x => x.DirectMessages).Returns(messagesDbSet.Object);

        var receiptsDbSet = new List<DirectMessageReceipt>().BuildMockDbSet();
        receiptsDbSet.Setup(x => x.Add(It.IsAny<DirectMessageReceipt>()));
        _mockContext.Setup(x => x.DirectMessageReceipts).Returns(receiptsDbSet.Object);

        var notificationsDbSet = new List<Notification>().BuildMockDbSet();
        notificationsDbSet.Setup(x => x.Add(It.IsAny<Notification>()));
        _mockContext.Setup(x => x.Notifications).Returns(notificationsDbSet.Object);

        _mockContext.Setup(x => x.SaveChangesAsync(CancellationToken.None)).ReturnsAsync(1);
        _mockNotifier.Setup(x => x.NotifyCreatedAsync(It.IsAny<IReadOnlyCollection<NotificationDto>>(), CancellationToken.None))
                     .Returns(Task.CompletedTask);

        var command = new RequestTaskReviewCommand(sessionId, mentorId, "Please review my work");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.ReviewId.Should().NotBeEmpty();
        result.Value.MessageId.Should().NotBeEmpty();
        result.Value.ConversationId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_SessionNotFound_ReturnsFailure()
    {
        // Arrange
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(NewId.NextGuid());

        var sessionsDbSet = new List<FocusSession>().BuildMockDbSet();
        _mockContext.Setup(x => x.FocusSessions).Returns(sessionsDbSet.Object);

        var command = new RequestTaskReviewCommand(NewId.NextGuid(), NewId.NextGuid(), null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("SESSION_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_ReviewAlreadyRequested_ReturnsFailure()
    {
        // Arrange
        var (studentId, mentorId, sessionId, taskId, _, _) =
            SetupValidData(out var session, out _, out _);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);

        var sessionsDbSet = new List<FocusSession> { session }.BuildMockDbSet();
        _mockContext.Setup(x => x.FocusSessions).Returns(sessionsDbSet.Object);

        var existingReview = new TaskReview
        {
            ReviewId = NewId.NextGuid(),
            SessionId = sessionId,
            TaskId = taskId,
            StudentId = studentId,
            MentorId = mentorId,
            Status = TaskReviewStatus.Pending
        };
        var reviewsDbSet = new List<TaskReview> { existingReview }.BuildMockDbSet();
        _mockContext.Setup(x => x.TaskReviews).Returns(reviewsDbSet.Object);

        var command = new RequestTaskReviewCommand(sessionId, mentorId, null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("REVIEW_ALREADY_REQUESTED");
    }

    [Fact]
    public async Task Handle_NoSubmission_ReturnsFailure()
    {
        // Arrange
        var studentId = NewId.NextGuid();
        var mentorId = NewId.NextGuid();
        var sessionId = NewId.NextGuid();
        var pathId = NewId.NextGuid();

        var learningPath = new LearningPath { PathId = pathId, UserId = studentId, Title = "Test Path" };
        var chapter = new Chapter { ChapterId = NewId.NextGuid(), PathId = pathId, Title = "Chapter 1" };
        var task = new Domain.Entities.Tasks
        {
            TaskId = NewId.NextGuid(),
            PathId = pathId,
            ChapterId = chapter.ChapterId,
            Title = "Task",
            LearningPath = learningPath,
            Chapter = chapter
        };

        var session = new FocusSession
        {
            SessionId = sessionId,
            TaskId = task.TaskId,
            Task = task,
            // No submission
            SubmittedCode = null,
            SubmittedSummary = null,
            SubmittedQuizAnswers = null
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);

        var sessionsDbSet = new List<FocusSession> { session }.BuildMockDbSet();
        _mockContext.Setup(x => x.FocusSessions).Returns(sessionsDbSet.Object);

        var reviewsDbSet = new List<TaskReview>().BuildMockDbSet();
        _mockContext.Setup(x => x.TaskReviews).Returns(reviewsDbSet.Object);

        var command = new RequestTaskReviewCommand(sessionId, mentorId, null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("NO_SUBMISSION");
    }

    [Fact]
    public async Task Handle_TaskReviewLimitReached_ReturnsFailure()
    {
        // Arrange
        var (studentId, mentorId, sessionId, _, subscriptionId, mentorPackageId) =
            SetupValidData(out var session, out var subscription, out var mentorPackage);

        subscription.TaskReviewLimit = 3;
        subscription.TaskReviewsUsed = 3; // at limit

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);

        var sessionsDbSet = new List<FocusSession> { session }.BuildMockDbSet();
        _mockContext.Setup(x => x.FocusSessions).Returns(sessionsDbSet.Object);

        var reviewsDbSet = new List<TaskReview>().BuildMockDbSet();
        _mockContext.Setup(x => x.TaskReviews).Returns(reviewsDbSet.Object);

        var subscriptionsDbSet = new List<StudentMentorSubscription> { subscription }.BuildMockDbSet();
        _mockContext.Setup(x => x.StudentMentorSubscriptions).Returns(subscriptionsDbSet.Object);

        var mentorPackagesDbSet = new List<MentorPackage> { mentorPackage }.BuildMockDbSet();
        _mockContext.Setup(x => x.MentorPackages).Returns(mentorPackagesDbSet.Object);

        var command = new RequestTaskReviewCommand(sessionId, mentorId, null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("TASK_REVIEW_LIMIT_REACHED");
    }

    [Fact]
    public async Task Handle_UnlimitedSubscription_IgnoresLimitCheck()
    {
        // Arrange
        var (studentId, mentorId, sessionId, _, subscriptionId, mentorPackageId) =
            SetupValidData(out var session, out var subscription, out var mentorPackage);

        subscription.TaskReviewLimit = -1; // unlimited
        subscription.TaskReviewsUsed = 999;

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);

        var sessionsDbSet = new List<FocusSession> { session }.BuildMockDbSet();
        _mockContext.Setup(x => x.FocusSessions).Returns(sessionsDbSet.Object);

        var reviewsDbSet = new List<TaskReview>().BuildMockDbSet();
        reviewsDbSet.Setup(x => x.Add(It.IsAny<TaskReview>()));
        _mockContext.Setup(x => x.TaskReviews).Returns(reviewsDbSet.Object);

        var subscriptionsDbSet = new List<StudentMentorSubscription> { subscription }.BuildMockDbSet();
        _mockContext.Setup(x => x.StudentMentorSubscriptions).Returns(subscriptionsDbSet.Object);

        var mentorPackagesDbSet = new List<MentorPackage> { mentorPackage }.BuildMockDbSet();
        _mockContext.Setup(x => x.MentorPackages).Returns(mentorPackagesDbSet.Object);

        var conversationsDbSet = new List<DirectConversation>().BuildMockDbSet();
        conversationsDbSet.Setup(x => x.Add(It.IsAny<DirectConversation>()));
        _mockContext.Setup(x => x.DirectConversations).Returns(conversationsDbSet.Object);

        var messagesDbSet = new List<DirectMessage>().BuildMockDbSet();
        messagesDbSet.Setup(x => x.Add(It.IsAny<DirectMessage>()));
        _mockContext.Setup(x => x.DirectMessages).Returns(messagesDbSet.Object);

        var receiptsDbSet = new List<DirectMessageReceipt>().BuildMockDbSet();
        receiptsDbSet.Setup(x => x.Add(It.IsAny<DirectMessageReceipt>()));
        _mockContext.Setup(x => x.DirectMessageReceipts).Returns(receiptsDbSet.Object);

        var notificationsDbSet = new List<Notification>().BuildMockDbSet();
        notificationsDbSet.Setup(x => x.Add(It.IsAny<Notification>()));
        _mockContext.Setup(x => x.Notifications).Returns(notificationsDbSet.Object);

        _mockContext.Setup(x => x.SaveChangesAsync(CancellationToken.None)).ReturnsAsync(1);
        _mockNotifier.Setup(x => x.NotifyCreatedAsync(It.IsAny<IReadOnlyCollection<NotificationDto>>(), CancellationToken.None))
                     .Returns(Task.CompletedTask);

        var command = new RequestTaskReviewCommand(sessionId, mentorId, null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }
}
