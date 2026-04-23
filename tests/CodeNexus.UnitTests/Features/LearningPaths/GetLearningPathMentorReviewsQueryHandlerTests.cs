using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPathMentorReviews.Queries.GetLearningPathMentorReviews;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.LearningPaths;

public class GetLearningPathMentorReviewsQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetLearningPathMentorReviewsQueryHandler _handler;

    public GetLearningPathMentorReviewsQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new GetLearningPathMentorReviewsQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_PathNotFound_ReturnsFailure()
    {
        var userId = Guid.NewGuid();
        var role = new Role { RoleId = Guid.NewGuid(), RoleName = "Student" };
        var user = new User { UserId = userId, RoleId = role.RoleId, Role = role };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Users).Returns(new[] { user }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathMentorReviews).Returns(new List<LearningPathMentorReview>().BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetLearningPathMentorReviewsQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("LEARNING_PATH_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_StudentOwner_ReturnsSortedReviewsAndStats()
    {
        var studentRole = new Role { RoleId = Guid.NewGuid(), RoleName = "Student" };
        var mentorRole = new Role { RoleId = Guid.NewGuid(), RoleName = "Mentor" };

        var studentId = Guid.NewGuid();
        var mentorId = Guid.NewGuid();
        var pathId = Guid.NewGuid();

        var student = new User
        {
            UserId = studentId,
            Username = "student",
            RoleId = studentRole.RoleId,
            Role = studentRole
        };

        var mentor = new User
        {
            UserId = mentorId,
            Username = "mentorA",
            RoleId = mentorRole.RoleId,
            Role = mentorRole
        };

        var path = new LearningPath
        {
            PathId = pathId,
            UserId = studentId,
            Title = "AI Path"
        };

        var now = DateTime.UtcNow;
        var reviewOlder = new LearningPathMentorReview
        {
            ReviewId = Guid.NewGuid(),
            PathId = pathId,
            MentorId = mentorId,
            StudentId = studentId,
            Score = 7,
            Feedback = "old",
            CreatedAt = now.AddHours(-3),
            UpdatedAt = null
        };

        var reviewNewer = new LearningPathMentorReview
        {
            ReviewId = Guid.NewGuid(),
            PathId = pathId,
            MentorId = mentorId,
            StudentId = studentId,
            Score = 9,
            Feedback = "new",
            CreatedAt = now.AddHours(-2),
            UpdatedAt = now.AddHours(-1)
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockContext.Setup(x => x.Users).Returns(new[] { student, mentor }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new[] { path }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathMentorReviews).Returns(new[] { reviewOlder, reviewNewer }.BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetLearningPathMentorReviewsQuery(pathId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.PathId.Should().Be(pathId);
        result.Value.TotalReviews.Should().Be(2);
        result.Value.AverageScore.Should().Be(8.0d);
        result.Value.Reviews.Should().HaveCount(2);
        result.Value.Reviews[0].ReviewId.Should().Be(reviewNewer.ReviewId);
        result.Value.Reviews[0].MentorName.Should().Be("mentorA");
    }

    [Fact]
    public async Task Handle_StudentNotOwner_ReturnsAccessDenied()
    {
        var role = new Role { RoleId = Guid.NewGuid(), RoleName = "Student" };

        var currentStudentId = Guid.NewGuid();
        var pathOwnerId = Guid.NewGuid();
        var pathId = Guid.NewGuid();

        var currentStudent = new User
        {
            UserId = currentStudentId,
            Username = "studentB",
            RoleId = role.RoleId,
            Role = role
        };

        var path = new LearningPath
        {
            PathId = pathId,
            UserId = pathOwnerId,
            Title = "Other Path"
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(currentStudentId);
        _mockContext.Setup(x => x.Users).Returns(new[] { currentStudent }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new[] { path }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathMentorReviews).Returns(new List<LearningPathMentorReview>().BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetLearningPathMentorReviewsQuery(pathId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("ACCESS_DENIED");
    }
}
