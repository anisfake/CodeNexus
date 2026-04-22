using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetPublishedLearningPaths;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;

namespace CodeNexus.UnitTests.Features.LearningPaths;

public class GetPublishedLearningPathsQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetPublishedLearningPathsQueryHandler _handler;

    public GetPublishedLearningPathsQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new GetPublishedLearningPathsQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_ReturnsOnlyPublishedPaths()
    {
        var studentId = NewId.NextGuid();
        var mentorId = NewId.NextGuid();
        var subjectId = NewId.NextGuid();
        var subject = new Subject { SubjectId = subjectId, Name = "C#" };
        var mentor = new User { UserId = mentorId, Username = "mentor" };

        var publishedPath = new LearningPath
        {
            PathId = NewId.NextGuid(),
            UserId = mentorId,
            User = mentor,
            SubjectId = subjectId,
            Subject = subject,
            Title = "Published Path",
            Status = LearningPathStatus.Published.ToString(),
            VersionNumber = 1.0m,
            ComplexityLevel = ComplexityLevel.Beginner,
            Language = LanguageSelection.VietNamese,
            Chapters = new List<Chapter>()
        };

        var draftPath = new LearningPath
        {
            PathId = NewId.NextGuid(),
            UserId = mentorId,
            User = mentor,
            SubjectId = subjectId,
            Subject = subject,
            Title = "Draft Path",
            Status = LearningPathStatus.Draft.ToString(),
            VersionNumber = 1.0m,
            ComplexityLevel = ComplexityLevel.Beginner,
            Language = LanguageSelection.VietNamese,
            Chapters = new List<Chapter>()
        };

        var pathsDb = new List<LearningPath> { publishedPath, draftPath }.BuildMockDbSet();
        var sharesDb = new List<LearningPathShare>().BuildMockDbSet();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockContext.Setup(x => x.LearningPaths).Returns(pathsDb.Object);
        _mockContext.Setup(x => x.LearningPathShares).Returns(sharesDb.Object);

        var result = await _handler.Handle(new GetPublishedLearningPathsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items[0].PathId.Should().Be(publishedPath.PathId);
    }

    [Fact]
    public async Task Handle_FilterBySubjectId_ReturnsMatchingPaths()
    {
        var studentId = NewId.NextGuid();
        var mentorId = NewId.NextGuid();
        var subjectA = new Subject { SubjectId = NewId.NextGuid(), Name = "C#" };
        var subjectB = new Subject { SubjectId = NewId.NextGuid(), Name = "Python" };
        var mentor = new User { UserId = mentorId, Username = "mentor" };

        var pathA = new LearningPath
        {
            PathId = NewId.NextGuid(),
            UserId = mentorId,
            User = mentor,
            SubjectId = subjectA.SubjectId,
            Subject = subjectA,
            Title = "C# Path",
            Status = LearningPathStatus.Published.ToString(),
            VersionNumber = 1.0m,
            ComplexityLevel = ComplexityLevel.Beginner,
            Language = LanguageSelection.VietNamese,
            Chapters = new List<Chapter>()
        };

        var pathB = new LearningPath
        {
            PathId = NewId.NextGuid(),
            UserId = mentorId,
            User = mentor,
            SubjectId = subjectB.SubjectId,
            Subject = subjectB,
            Title = "Python Path",
            Status = LearningPathStatus.Published.ToString(),
            VersionNumber = 1.0m,
            ComplexityLevel = ComplexityLevel.Beginner,
            Language = LanguageSelection.VietNamese,
            Chapters = new List<Chapter>()
        };

        var pathsDb = new List<LearningPath> { pathA, pathB }.BuildMockDbSet();
        var sharesDb = new List<LearningPathShare>().BuildMockDbSet();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockContext.Setup(x => x.LearningPaths).Returns(pathsDb.Object);
        _mockContext.Setup(x => x.LearningPathShares).Returns(sharesDb.Object);

        var result = await _handler.Handle(new GetPublishedLearningPathsQuery(SubjectId: subjectA.SubjectId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items[0].SubjectId.Should().Be(subjectA.SubjectId);
    }

    [Fact]
    public async Task Handle_FilterByComplexityLevel_ReturnsMatchingPaths()
    {
        var studentId = NewId.NextGuid();
        var mentorId = NewId.NextGuid();
        var subject = new Subject { SubjectId = NewId.NextGuid(), Name = "C#" };
        var mentor = new User { UserId = mentorId, Username = "mentor" };

        var beginnerPath = new LearningPath
        {
            PathId = NewId.NextGuid(),
            UserId = mentorId,
            User = mentor,
            SubjectId = subject.SubjectId,
            Subject = subject,
            Title = "Beginner Path",
            Status = LearningPathStatus.Published.ToString(),
            VersionNumber = 1.0m,
            ComplexityLevel = ComplexityLevel.Beginner,
            Language = LanguageSelection.VietNamese,
            Chapters = new List<Chapter>()
        };

        var advancedPath = new LearningPath
        {
            PathId = NewId.NextGuid(),
            UserId = mentorId,
            User = mentor,
            SubjectId = subject.SubjectId,
            Subject = subject,
            Title = "Advanced Path",
            Status = LearningPathStatus.Published.ToString(),
            VersionNumber = 1.0m,
            ComplexityLevel = ComplexityLevel.Advanced,
            Language = LanguageSelection.VietNamese,
            Chapters = new List<Chapter>()
        };

        var pathsDb = new List<LearningPath> { beginnerPath, advancedPath }.BuildMockDbSet();
        var sharesDb = new List<LearningPathShare>().BuildMockDbSet();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockContext.Setup(x => x.LearningPaths).Returns(pathsDb.Object);
        _mockContext.Setup(x => x.LearningPathShares).Returns(sharesDb.Object);

        var result = await _handler.Handle(
            new GetPublishedLearningPathsQuery(ComplexityLevel: ComplexityLevel.Beginner),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        result.Value.Items[0].ComplexityLevel.Should().Be(ComplexityLevel.Beginner);
    }

    [Fact]
    public async Task Handle_StudentAlreadyEnrolled_ReturnsIsEnrolledTrue()
    {
        var studentId = NewId.NextGuid();
        var mentorId = NewId.NextGuid();
        var pathId = NewId.NextGuid();
        var subject = new Subject { SubjectId = NewId.NextGuid(), Name = "C#" };
        var mentor = new User { UserId = mentorId, Username = "mentor" };

        var publishedPath = new LearningPath
        {
            PathId = pathId,
            UserId = mentorId,
            User = mentor,
            SubjectId = subject.SubjectId,
            Subject = subject,
            Title = "Published Path",
            Status = LearningPathStatus.Published.ToString(),
            VersionNumber = 1.0m,
            ComplexityLevel = ComplexityLevel.Beginner,
            Language = LanguageSelection.VietNamese,
            Chapters = new List<Chapter>()
        };

        var enrollment = new LearningPathShare
        {
            ShareId = NewId.NextGuid(),
            PathId = pathId,
            StudentId = studentId,
            MentorId = mentorId,
            Status = LearningPathShareStatus.Accepted
        };

        var pathsDb = new List<LearningPath> { publishedPath }.BuildMockDbSet();
        var sharesDb = new List<LearningPathShare> { enrollment }.BuildMockDbSet();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockContext.Setup(x => x.LearningPaths).Returns(pathsDb.Object);
        _mockContext.Setup(x => x.LearningPathShares).Returns(sharesDb.Object);

        var result = await _handler.Handle(new GetPublishedLearningPathsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items[0].IsEnrolled.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_StudentNotEnrolled_ReturnsIsEnrolledFalse()
    {
        var studentId = NewId.NextGuid();
        var mentorId = NewId.NextGuid();
        var pathId = NewId.NextGuid();
        var subject = new Subject { SubjectId = NewId.NextGuid(), Name = "C#" };
        var mentor = new User { UserId = mentorId, Username = "mentor" };

        var publishedPath = new LearningPath
        {
            PathId = pathId,
            UserId = mentorId,
            User = mentor,
            SubjectId = subject.SubjectId,
            Subject = subject,
            Title = "Published Path",
            Status = LearningPathStatus.Published.ToString(),
            VersionNumber = 1.0m,
            ComplexityLevel = ComplexityLevel.Beginner,
            Language = LanguageSelection.VietNamese,
            Chapters = new List<Chapter>()
        };

        var pathsDb = new List<LearningPath> { publishedPath }.BuildMockDbSet();
        var sharesDb = new List<LearningPathShare>().BuildMockDbSet();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockContext.Setup(x => x.LearningPaths).Returns(pathsDb.Object);
        _mockContext.Setup(x => x.LearningPathShares).Returns(sharesDb.Object);

        var result = await _handler.Handle(new GetPublishedLearningPathsQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items[0].IsEnrolled.Should().BeFalse();
    }
}
