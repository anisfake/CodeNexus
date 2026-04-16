using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPathShares.Queries.GetLearningPathShareUpdateContext;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;

namespace CodeNexus.UnitTests.Features.LearningPathShares;

public class GetLearningPathShareUpdateContextQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetLearningPathShareUpdateContextQueryHandler _handler;

    public GetLearningPathShareUpdateContextQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new GetLearningPathShareUpdateContextQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_AcceptedShareWithDifferences_ReturnsChangeSummary()
    {
        var mentorId = NewId.NextGuid();
        var studentId = NewId.NextGuid();
        var shareId = NewId.NextGuid();
        var sourcePathId = NewId.NextGuid();
        var acceptedPathId = NewId.NextGuid();

        var mentor = new User
        {
            UserId = mentorId,
            Username = "mentor-1",
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" }
        };

        var share = new LearningPathShare
        {
            ShareId = shareId,
            PathId = sourcePathId,
            MentorId = mentorId,
            StudentId = studentId,
            AcceptedPathId = acceptedPathId,
            SourceVersionAtAccept = 1,
            Status = LearningPathShareStatus.Accepted,
            Mentor = mentor,
            SentAt = DateTime.UtcNow
        };

        var sourcePath = new LearningPath
        {
            PathId = sourcePathId,
            UserId = mentorId,
            SubjectId = NewId.NextGuid(),
            Title = "Source Path",
            VersionNumber = 2,
            Chapters = new List<Chapter>
            {
                new()
                {
                    ChapterId = NewId.NextGuid(),
                    PathId = sourcePathId,
                    Title = "Chapter Basics",
                    Content = "New content",
                    OrderIndex = 0,
                    Lessons = new List<Lesson>
                    {
                        new() { LessonId = NewId.NextGuid(), Title = "Intro Updated", Content = "new", OrderIndex = 0 },
                        new() { LessonId = NewId.NextGuid(), Title = "Extra Lesson", Content = "extra", OrderIndex = 1 }
                    }
                },
                new()
                {
                    ChapterId = NewId.NextGuid(),
                    PathId = sourcePathId,
                    Title = "New Chapter",
                    OrderIndex = 1,
                    Lessons = new List<Lesson>()
                }
            }
        };

        var acceptedPath = new LearningPath
        {
            PathId = acceptedPathId,
            UserId = studentId,
            SubjectId = sourcePath.SubjectId,
            Title = "Student Path",
            VersionNumber = 1,
            Chapters = new List<Chapter>
            {
                new()
                {
                    ChapterId = NewId.NextGuid(),
                    PathId = acceptedPathId,
                    Title = "Chapter Old",
                    Content = "old content",
                    OrderIndex = 0,
                    Lessons = new List<Lesson>
                    {
                        new() { LessonId = NewId.NextGuid(), Title = "Intro", Content = "old", OrderIndex = 0 }
                    }
                },
                new()
                {
                    ChapterId = NewId.NextGuid(),
                    PathId = acceptedPathId,
                    Title = "Removed Chapter",
                    OrderIndex = 2,
                    Lessons = new List<Lesson>()
                }
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockContext.Setup(x => x.LearningPathShares).Returns(new List<LearningPathShare> { share }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath> { sourcePath, acceptedPath }.BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetLearningPathShareUpdateContextQuery(shareId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.HasNewVersion.Should().BeTrue();
        result.Value.ChangeSummary.Should().NotBeNull();
        result.Value.ChangeSummary!.AddedChapterCount.Should().Be(1);
        result.Value.ChangeSummary.RemovedChapterCount.Should().Be(1);
        result.Value.ChangeSummary.UpdatedChapterCount.Should().Be(1);
        result.Value.ChangeSummary.AddedLessonCount.Should().Be(1);
        result.Value.ChangeSummary.UpdatedLessonCount.Should().Be(1);
        result.Value.ChangeSummary.AddedChapters.Should().Contain("New Chapter");
        result.Value.ChangeSummary.RemovedChapters.Should().Contain("Removed Chapter");
    }

    [Fact]
    public async Task Handle_OnlyLessonContentDifferent_MarksLessonUpdated()
    {
        var mentorId = NewId.NextGuid();
        var studentId = NewId.NextGuid();
        var shareId = NewId.NextGuid();
        var sourcePathId = NewId.NextGuid();
        var acceptedPathId = NewId.NextGuid();

        var mentor = new User
        {
            UserId = mentorId,
            Username = "mentor-1",
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" }
        };

        var share = new LearningPathShare
        {
            ShareId = shareId,
            PathId = sourcePathId,
            MentorId = mentorId,
            StudentId = studentId,
            AcceptedPathId = acceptedPathId,
            SourceVersionAtAccept = 1,
            Status = LearningPathShareStatus.Accepted,
            Mentor = mentor,
            SentAt = DateTime.UtcNow
        };

        var sourcePath = new LearningPath
        {
            PathId = sourcePathId,
            UserId = mentorId,
            SubjectId = NewId.NextGuid(),
            Title = "Source Path",
            VersionNumber = 2,
            Chapters = new List<Chapter>
            {
                new()
                {
                    ChapterId = NewId.NextGuid(),
                    PathId = sourcePathId,
                    Title = "Chapter Basics",
                    OrderIndex = 0,
                    Lessons = new List<Lesson>
                    {
                        new() { LessonId = NewId.NextGuid(), Title = "Intro", Content = "mentor content", OrderIndex = 0 }
                    }
                }
            }
        };

        var acceptedPath = new LearningPath
        {
            PathId = acceptedPathId,
            UserId = studentId,
            SubjectId = sourcePath.SubjectId,
            Title = "Student Path",
            VersionNumber = 1,
            Chapters = new List<Chapter>
            {
                new()
                {
                    ChapterId = NewId.NextGuid(),
                    PathId = acceptedPathId,
                    Title = "Chapter Basics",
                    OrderIndex = 0,
                    Lessons = new List<Lesson>
                    {
                        new() { LessonId = NewId.NextGuid(), Title = "Intro", Content = "student local content", OrderIndex = 0 }
                    }
                }
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockContext.Setup(x => x.LearningPathShares).Returns(new List<LearningPathShare> { share }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath> { sourcePath, acceptedPath }.BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetLearningPathShareUpdateContextQuery(shareId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.ChangeSummary.Should().NotBeNull();
        result.Value.ChangeSummary!.UpdatedLessonCount.Should().Be(1);
        result.Value.ChangeSummary.UpdatedLessons.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_AcceptedShareWithoutAcceptedPath_ReturnsNoChangeSummary()
    {
        var mentorId = NewId.NextGuid();
        var studentId = NewId.NextGuid();
        var shareId = NewId.NextGuid();
        var sourcePathId = NewId.NextGuid();

        var mentor = new User
        {
            UserId = mentorId,
            Username = "mentor-1",
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" }
        };

        var share = new LearningPathShare
        {
            ShareId = shareId,
            PathId = sourcePathId,
            MentorId = mentorId,
            StudentId = studentId,
            AcceptedPathId = null,
            SourceVersionAtAccept = 1,
            Status = LearningPathShareStatus.Accepted,
            Mentor = mentor,
            SentAt = DateTime.UtcNow
        };

        var sourcePath = new LearningPath
        {
            PathId = sourcePathId,
            UserId = mentorId,
            SubjectId = NewId.NextGuid(),
            Title = "Source Path",
            VersionNumber = 2,
            Chapters = new List<Chapter>()
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(studentId);
        _mockContext.Setup(x => x.LearningPathShares).Returns(new List<LearningPathShare> { share }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new List<LearningPath> { sourcePath }.BuildMockDbSet().Object);

        var result = await _handler.Handle(new GetLearningPathShareUpdateContextQuery(shareId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.HasNewVersion.Should().BeTrue();
        result.Value.ChangeSummary.Should().BeNull();
    }
}
