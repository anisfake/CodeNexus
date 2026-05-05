using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPathMentorReviews.Commands.RespondLearningPathMentorReview;
using CodeNexus.Application.Features.LearningPathShares.Services;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.LearningPaths;

public class RespondLearningPathMentorReviewCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<ILearningPathSharePathSyncService> _mockPathSyncService;
    private readonly Mock<IDateTimeProvider> _mockDateTimeProvider;
    private readonly RespondLearningPathMentorReviewCommandHandler _handler;

    public RespondLearningPathMentorReviewCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockPathSyncService = new Mock<ILearningPathSharePathSyncService>();
        _mockDateTimeProvider = new Mock<IDateTimeProvider>();

        _handler = new RespondLearningPathMentorReviewCommandHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object,
            _mockPathSyncService.Object,
            _mockDateTimeProvider.Object);
    }

    [Fact]
    public async Task Handle_Reject_IncrementsValidationRequestsUsed()
    {
        var studentRole = new Role { RoleId = Guid.NewGuid(), RoleName = "Student" };
        var studentId = Guid.NewGuid();
        var mentorId = Guid.NewGuid();
        var pathId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var packageId = Guid.NewGuid();

        var student = new User { UserId = studentId, RoleId = studentRole.RoleId, Role = studentRole };
        var path = new LearningPath { PathId = pathId, UserId = studentId, Title = "Path" };
        var review = new LearningPathMentorReview
        {
            ReviewId = reviewId,
            PathId = pathId,
            MentorId = mentorId,
            StudentId = studentId
        };
        var pkg = new MentorPackage { MentorPackageId = packageId, ValidationRequestLimit = 2 };
        var studentSub = new StudentMentorSubscription
        {
            SubscriptionId = Guid.NewGuid(),
            UserId = studentId,
            MentorPackageId = packageId,
            ValidationRequestsUsed = 0,
            MentorPackage = pkg
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockContext.Setup(x => x.Users).Returns(new[] { student }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new[] { path }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathMentorReviews).Returns(new[] { review }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.StudentMentorSubscriptions).Returns(new[] { studentSub }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.MentorPackages).Returns(new[] { pkg }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(
            new RespondLearningPathMentorReviewCommand(
                pathId,
                reviewId,
                LearningPathMentorReviewDecisionStatus.Rejected,
                "Chưa ổn"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.DecisionStatus.Should().Be(LearningPathMentorReviewDecisionStatus.Rejected);
        result.Value.ValidationRequestsUsed.Should().Be(1);
        result.Value.ValidationRequestLimit.Should().Be(2);
        result.Value.CanRequestValidation.Should().BeTrue();
        studentSub.ValidationRequestsUsed.Should().Be(1);

        _mockPathSyncService.Verify(x => x.RebuildCurrentPathFromSourceAsync(It.IsAny<LearningPath>(), It.IsAny<LearningPath>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Accept_RebuildsStudentPathFromMentorWorkspace()
    {
        var studentRole = new Role { RoleId = Guid.NewGuid(), RoleName = "Student" };
        var studentId = Guid.NewGuid();
        var mentorId = Guid.NewGuid();
        var pathId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        var revisedPathId = Guid.NewGuid();

        var student = new User { UserId = studentId, RoleId = studentRole.RoleId, Role = studentRole };

        var targetPath = new LearningPath
        {
            PathId = pathId,
            UserId = studentId,
            SubjectId = Guid.NewGuid(),
            Title = "Current Student Path",
            LearningPathGoals = new List<LearningPathGoal>(),
            Chapters = new List<Chapter>()
        };

        var sourcePath = new LearningPath
        {
            PathId = revisedPathId,
            UserId = mentorId,
            SubjectId = targetPath.SubjectId,
            Title = "Mentor Revised Path",
            LearningPathGoals = new List<LearningPathGoal>(),
            Chapters = new List<Chapter>()
        };

        var review = new LearningPathMentorReview
        {
            ReviewId = reviewId,
            PathId = pathId,
            RevisedPathId = revisedPathId,
            MentorId = mentorId,
            StudentId = studentId
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockDateTimeProvider.Setup(x => x.UtcNow).Returns(new DateTime(2026, 4, 23, 10, 0, 0, DateTimeKind.Utc));
        _mockContext.Setup(x => x.Users).Returns(new[] { student }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new[] { targetPath, sourcePath }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathMentorReviews).Returns(new[] { review }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.StudentMentorSubscriptions).Returns(new List<StudentMentorSubscription>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(
            new RespondLearningPathMentorReviewCommand(
                pathId,
                reviewId,
                LearningPathMentorReviewDecisionStatus.Accepted,
                "Ok nhận"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.DecisionStatus.Should().Be(LearningPathMentorReviewDecisionStatus.Accepted);
        result.Value.ValidationRequestsUsed.Should().Be(0);
        result.Value.CanRequestValidation.Should().BeTrue();

        _mockPathSyncService.Verify(
            x => x.RebuildCurrentPathFromSourceAsync(
                It.Is<LearningPath>(p => p.PathId == pathId),
                It.Is<LearningPath>(p => p.PathId == revisedPathId),
                studentId,
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

