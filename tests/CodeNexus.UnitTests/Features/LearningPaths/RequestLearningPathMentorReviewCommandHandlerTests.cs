using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPathMentorReviews.Commands.RequestLearningPathMentorReview;
using CodeNexus.Application.Features.LearningPathShares.Services;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.LearningPaths;

public class RequestLearningPathMentorReviewCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<ILearningPathSharePathSyncService> _mockPathSyncService;
    private readonly Mock<IDateTimeProvider> _mockDateTimeProvider;
    private readonly RequestLearningPathMentorReviewCommandHandler _handler;

    public RequestLearningPathMentorReviewCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockPathSyncService = new Mock<ILearningPathSharePathSyncService>();
        _mockDateTimeProvider = new Mock<IDateTimeProvider>();

        _handler = new RequestLearningPathMentorReviewCommandHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object,
            _mockPathSyncService.Object,
            _mockDateTimeProvider.Object);
    }

    [Fact]
    public async Task Handle_ValidRequest_CreatesReviewAndWorkspace()
    {
        var studentRole = new Role { RoleId = Guid.NewGuid(), RoleName = "Student" };
        var mentorRole = new Role { RoleId = Guid.NewGuid(), RoleName = "Mentor" };

        var studentId = Guid.NewGuid();
        var mentorId = Guid.NewGuid();
        var pathId = Guid.NewGuid();
        var revisedPathId = Guid.NewGuid();
        var packageId = Guid.NewGuid();

        var student = new User { UserId = studentId, RoleId = studentRole.RoleId, Role = studentRole };
        var mentor = new User { UserId = mentorId, RoleId = mentorRole.RoleId, Role = mentorRole };
        var package = new MentorPackage
        {
            MentorPackageId = packageId,
            Name = "Mentor Plus",
            ValidationRequestLimit = 4
        };
        var subscription = new StudentMentorSubscription
        {
            SubscriptionId = Guid.NewGuid(),
            UserId = studentId,
            MentorPackageId = packageId,
            MentorPackage = package,
            IsActive = true,
            ValidationRequestsUsed = 0,
            CreatedAt = DateTime.UtcNow
        };

        var sourcePath = new LearningPath
        {
            PathId = pathId,
            UserId = studentId,
            SubjectId = Guid.NewGuid(),
            Title = "Student AI Path",
            Description = "desc",
            Status = LearningPathStatus.Active.ToString(),
            LearningPathGoals = new List<LearningPathGoal>(),
            Chapters = new List<Chapter>()
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockDateTimeProvider.Setup(x => x.UtcNow).Returns(new DateTime(2026, 4, 23, 8, 0, 0, DateTimeKind.Utc));

        _mockContext.Setup(x => x.Users).Returns(new[] { student, mentor }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new[] { sourcePath }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.StudentMentorSubscriptions).Returns(new[] { subscription }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathMentorReviews).Returns(new List<LearningPathMentorReview>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.MentorPackages).Returns(new[] { package }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _mockPathSyncService
            .Setup(x => x.ClonePathForStudentAsync(
                It.IsAny<LearningPath>(),
                mentorId,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(revisedPathId);

        var command = new RequestLearningPathMentorReviewCommand(
            pathId,
            mentorId,
            "Nho mentor review giup em");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.PathId.Should().Be(pathId);
        result.Value.MentorId.Should().Be(mentorId);
        result.Value.RevisedPathId.Should().Be(revisedPathId);
        result.Value.DecisionStatus.Should().Be(LearningPathMentorReviewDecisionStatus.Pending);
        result.Value.ValidationRequestsUsed.Should().Be(1);
        result.Value.ValidationRequestLimit.Should().Be(package.ValidationRequestLimit);
        result.Value.CanRequestValidation.Should().BeTrue();
        subscription.ValidationRequestsUsed.Should().Be(1);

        _mockPathSyncService.Verify(
            x => x.ClonePathForStudentAsync(
                It.Is<LearningPath>(p => p.PathId == pathId),
                mentorId,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_LimitReached_ReturnsFailure()
    {
        var studentRole = new Role { RoleId = Guid.NewGuid(), RoleName = "Student" };
        var mentorRole = new Role { RoleId = Guid.NewGuid(), RoleName = "Mentor" };

        var studentId = Guid.NewGuid();
        var mentorId = Guid.NewGuid();
        var pathId = Guid.NewGuid();
        var packageId = Guid.NewGuid();

        var student = new User { UserId = studentId, RoleId = studentRole.RoleId, Role = studentRole };
        var mentor = new User { UserId = mentorId, RoleId = mentorRole.RoleId, Role = mentorRole };
        var package = new MentorPackage
        {
            MentorPackageId = packageId,
            Name = "Mentor Standard",
            ValidationRequestLimit = 3
        };
        var subscription = new StudentMentorSubscription
        {
            SubscriptionId = Guid.NewGuid(),
            UserId = studentId,
            MentorPackageId = packageId,
            MentorPackage = package,
            IsActive = true,
            ValidationRequestsUsed = 3,
            CreatedAt = DateTime.UtcNow
        };

        var sourcePath = new LearningPath
        {
            PathId = pathId,
            UserId = studentId,
            SubjectId = Guid.NewGuid(),
            Title = "Student Path",
            LearningPathGoals = new List<LearningPathGoal>(),
            Chapters = new List<Chapter>()
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockDateTimeProvider.Setup(x => x.UtcNow).Returns(DateTime.UtcNow);

        _mockContext.Setup(x => x.Users).Returns(new[] { student, mentor }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new[] { sourcePath }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.StudentMentorSubscriptions).Returns(new[] { subscription }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.MentorPackages).Returns(new[] { package }.BuildMockDbSet().Object);

        var result = await _handler.Handle(
            new RequestLearningPathMentorReviewCommand(pathId, mentorId, "retry"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("VALIDATION_REQUEST_LIMIT_REACHED");

        _mockPathSyncService.Verify(
            x => x.ClonePathForStudentAsync(
                It.IsAny<LearningPath>(),
                It.IsAny<Guid>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_NoActiveSubscription_ReturnsSubscriptionRequired()
    {
        var studentRole = new Role { RoleId = Guid.NewGuid(), RoleName = "Student" };
        var mentorRole = new Role { RoleId = Guid.NewGuid(), RoleName = "Mentor" };

        var studentId = Guid.NewGuid();
        var mentorId = Guid.NewGuid();
        var pathId = Guid.NewGuid();

        var student = new User { UserId = studentId, RoleId = studentRole.RoleId, Role = studentRole };
        var mentor = new User { UserId = mentorId, RoleId = mentorRole.RoleId, Role = mentorRole };

        var sourcePath = new LearningPath
        {
            PathId = pathId,
            UserId = studentId,
            SubjectId = Guid.NewGuid(),
            Title = "Student Path",
            LearningPathGoals = new List<LearningPathGoal>(),
            Chapters = new List<Chapter>()
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockDateTimeProvider.Setup(x => x.UtcNow).Returns(DateTime.UtcNow);

        _mockContext.Setup(x => x.Users).Returns(new[] { student, mentor }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new[] { sourcePath }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.StudentMentorSubscriptions).Returns(new List<StudentMentorSubscription>().BuildMockDbSet().Object);

        var result = await _handler.Handle(
            new RequestLearningPathMentorReviewCommand(pathId, mentorId, "review giup"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("MENTOR_SUBSCRIPTION_REQUIRED");
    }
}

