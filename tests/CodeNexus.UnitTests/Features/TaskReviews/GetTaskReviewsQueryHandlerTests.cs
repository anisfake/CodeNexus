using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.TaskReviews.Queries.GetTaskReviews;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;

namespace CodeNexus.UnitTests.Features.TaskReviews;

public class GetTaskReviewsQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetTaskReviewsQueryHandler _handler;

    public GetTaskReviewsQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new GetTaskReviewsQueryHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object);
    }

    private TaskReview CreateReview(
        Guid mentorId,
        Guid studentId,
        TaskReviewStatus status = TaskReviewStatus.Pending,
        DateTime? requestedAt = null)
    {
        var taskId = NewId.NextGuid();
        var sessionId = NewId.NextGuid();

        return new TaskReview
        {
            ReviewId = NewId.NextGuid(),
            SessionId = sessionId,
            TaskId = taskId,
            StudentId = studentId,
            MentorId = mentorId,
            SubscriptionId = NewId.NextGuid(),
            Status = status,
            RequestedAt = requestedAt ?? DateTime.UtcNow,
            Task = new Domain.Entities.Tasks { TaskId = taskId, Title = "Test Task" },
            Student = new User
            {
                UserId = studentId,
                Username = "student01",
                UserProfile = new UserProfile { UserId = studentId, AvatarUrl = null }
            },
            Mentor = new User
            {
                UserId = mentorId,
                Username = "mentor01",
                UserProfile = new UserProfile { UserId = mentorId, AvatarUrl = null }
            }
        };
    }

    [Fact]
    public async Task Handle_AsMentor_ReturnsPendingReviewsNewestFirst()
    {
        // Arrange
        var mentorId = NewId.NextGuid();
        var studentId = NewId.NextGuid();

        var older = CreateReview(mentorId, studentId, TaskReviewStatus.Pending, DateTime.UtcNow.AddHours(-2));
        var newer = CreateReview(mentorId, studentId, TaskReviewStatus.Pending, DateTime.UtcNow.AddHours(-1));
        var reviewed = CreateReview(mentorId, studentId, TaskReviewStatus.Reviewed);
        var otherMentor = CreateReview(NewId.NextGuid(), studentId, TaskReviewStatus.Pending);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.TaskReviews).Returns(
            new List<TaskReview> { older, newer, reviewed, otherMentor }.BuildMockDbSet().Object);

        var query = new GetTaskReviewsQuery("Pending", 1, 20);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        result.Value.Items[0].ReviewId.Should().Be(newer.ReviewId); // newest first
        result.Value.Items[1].ReviewId.Should().Be(older.ReviewId);
        result.Value.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_AsStudent_ReturnsOwnPendingReviews()
    {
        // Arrange
        var studentId = NewId.NextGuid();
        var mentorId = NewId.NextGuid();

        var myPending = CreateReview(mentorId, studentId, TaskReviewStatus.Pending);
        var otherStudentReview = CreateReview(mentorId, NewId.NextGuid(), TaskReviewStatus.Pending);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockContext.Setup(x => x.TaskReviews).Returns(
            new List<TaskReview> { myPending, otherStudentReview }.BuildMockDbSet().Object);

        var query = new GetTaskReviewsQuery("Pending", 1, 20);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items[0].ReviewId.Should().Be(myPending.ReviewId);
    }

    [Fact]
    public async Task Handle_StatusReviewed_ReturnsReviewedOnly()
    {
        // Arrange
        var mentorId = NewId.NextGuid();
        var studentId = NewId.NextGuid();

        var pending = CreateReview(mentorId, studentId, TaskReviewStatus.Pending);
        var reviewed = CreateReview(mentorId, studentId, TaskReviewStatus.Reviewed);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.TaskReviews).Returns(
            new List<TaskReview> { pending, reviewed }.BuildMockDbSet().Object);

        var query = new GetTaskReviewsQuery("Reviewed", 1, 20);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items[0].Status.Should().Be("Reviewed");
    }

    [Fact]
    public async Task Handle_NullStatus_DefaultsToPending()
    {
        // Arrange
        var mentorId = NewId.NextGuid();
        var studentId = NewId.NextGuid();

        var pending = CreateReview(mentorId, studentId, TaskReviewStatus.Pending);
        var reviewed = CreateReview(mentorId, studentId, TaskReviewStatus.Reviewed);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.TaskReviews).Returns(
            new List<TaskReview> { pending, reviewed }.BuildMockDbSet().Object);

        // null status → defaults to Pending
        var query = new GetTaskReviewsQuery(null, 1, 20);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items[0].Status.Should().Be("Pending");
    }

    [Fact]
    public async Task Handle_Pagination_ReturnsCorrectPage()
    {
        // Arrange
        var mentorId = NewId.NextGuid();
        var studentId = NewId.NextGuid();

        var reviews = Enumerable.Range(0, 5)
            .Select(i => CreateReview(mentorId, studentId, TaskReviewStatus.Pending,
                DateTime.UtcNow.AddMinutes(-i)))
            .ToList();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.TaskReviews).Returns(reviews.BuildMockDbSet().Object);

        var query = new GetTaskReviewsQuery("Pending", 1, 3);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(3);
        result.Value.TotalCount.Should().Be(5);
        result.Value.TotalPages.Should().Be(2);
        result.Value.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_NoReviews_ReturnsEmptyPage()
    {
        // Arrange
        var mentorId = NewId.NextGuid();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.TaskReviews).Returns(
            new List<TaskReview>().BuildMockDbSet().Object);

        var query = new GetTaskReviewsQuery("Pending", 1, 20);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }
}
