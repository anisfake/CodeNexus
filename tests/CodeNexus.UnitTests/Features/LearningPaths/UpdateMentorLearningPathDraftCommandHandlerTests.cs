using CodeNexus.Application.Common.Events;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.LearningPathSkeleton.Commands.UpdateMentorLearningPathDraft;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using MediatR;
using Moq;
using GoalEntity = CodeNexus.Domain.Entities.Goals;

namespace CodeNexus.UnitTests.Features.LearningPaths;

public class UpdateMentorLearningPathDraftCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<IPublisher> _mockPublisher;
    private readonly UpdateMentorLearningPathDraftCommandHandler _handler;

    public UpdateMentorLearningPathDraftCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockPublisher = new Mock<IPublisher>();

        _mockPublisher
            .Setup(x => x.Publish(It.IsAny<LearningPathDraftVersionUpdatedEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new UpdateMentorLearningPathDraftCommandHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object,
            _mockPublisher.Object);
    }

    [Fact]
    public async Task Handle_NoMeaningfulChanges_DoesNotSaveOrPublish()
    {
        var mentorId = NewId.NextGuid();
        var subjectId = NewId.NextGuid();
        var goalId = NewId.NextGuid();
        var pathId = NewId.NextGuid();
        var chapterStart = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc);
        var chapterEnd = new DateTime(2026, 4, 16, 0, 0, 0, DateTimeKind.Utc);
        var lessonDay = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc);

        var mentor = new User
        {
            UserId = mentorId,
            Username = "mentor_a",
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" }
        };

        var goal = new GoalEntity
        {
            GoalId = goalId,
            Title = "Master TS",
            IsSystemDefined = false,
            IsActive = true,
            CreatedByUserId = mentorId
        };

        var learningPath = new LearningPath
        {
            PathId = pathId,
            UserId = mentorId,
            SubjectId = subjectId,
            Title = "TypeScript Path - ver 5",
            Description = "Draft description",
            StartDate = chapterStart,
            EndDate = chapterEnd,
            Status = LearningPathStatus.Draft.ToString(),
            VersionNumber = 5,
            ComplexityLevel = ComplexityLevel.Intermediate,
            Language = LanguageSelection.English,
            LearningPathGoals = new List<LearningPathGoal>
            {
                new() { PathId = pathId, GoalId = goalId, Goal = goal, Weight = 100m }
            },
            Chapters = new List<Chapter>
            {
                new()
                {
                    ChapterId = NewId.NextGuid(),
                    PathId = pathId,
                    Title = "Chapter 1",
                    OrderIndex = 0,
                    StartDate = chapterStart,
                    EndDate = chapterEnd,
                    EstimatedDays = 7,
                    IsDeleted = false,
                    Lessons = new List<Lesson>
                    {
                        new()
                        {
                            LessonId = NewId.NextGuid(),
                            ChapterId = NewId.NextGuid(),
                            Title = "Lesson 1",
                            OrderIndex = 0,
                            LessonDay = lessonDay,
                            IsDeleted = false
                        }
                    }
                }
            }
        };

        var command = new UpdateMentorLearningPathDraftCommand(
            pathId,
            5,
            subjectId,
            new List<LearningPathGoalRequest> { new(goalId, 100m) },
            ComplexityLevel.Intermediate,
            LanguageSelection.English,
            "TypeScript Path",
            "Draft description",
            chapterStart,
            chapterEnd,
            new List<ManualChapterRequest>
            {
                new("Chapter 1", chapterStart, chapterEnd, null,
                    new List<ManualLessonRequest>
                    {
                        new("Lesson 1", lessonDay)
                    })
            });

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(new[] { mentor }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new[] { learningPath }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Subjects).Returns(new[] { new Subject { SubjectId = subjectId, Name = "Programming" } }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Goals).Returns(new[] { goal }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SubjectGoals).Returns(new List<SubjectGoal>().BuildMockDbSet().Object);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Title.Should().Be("TypeScript Path - ver 5");
        learningPath.VersionNumber.Should().Be(5);

        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockPublisher.Verify(
            x => x.Publish(It.IsAny<LearningPathDraftVersionUpdatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithMeaningfulChanges_SavesAndPublishesUpdate()
    {
        var mentorId = NewId.NextGuid();
        var subjectId = NewId.NextGuid();
        var goalId = NewId.NextGuid();
        var pathId = NewId.NextGuid();
        var chapterStart = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc);
        var chapterEnd = new DateTime(2026, 4, 16, 0, 0, 0, DateTimeKind.Utc);
        var lessonDay = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc);

        var mentor = new User
        {
            UserId = mentorId,
            Username = "mentor_a",
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" }
        };

        var goal = new GoalEntity
        {
            GoalId = goalId,
            Title = "Master TS",
            IsSystemDefined = false,
            IsActive = true,
            CreatedByUserId = mentorId
        };

        var learningPath = new LearningPath
        {
            PathId = pathId,
            UserId = mentorId,
            SubjectId = subjectId,
            Title = "TypeScript Path - ver 5",
            Description = "Draft description",
            StartDate = chapterStart,
            EndDate = chapterEnd,
            Status = LearningPathStatus.Draft.ToString(),
            VersionNumber = 5,
            ComplexityLevel = ComplexityLevel.Intermediate,
            Language = LanguageSelection.English,
            LearningPathGoals = new List<LearningPathGoal>
            {
                new() { PathId = pathId, GoalId = goalId, Goal = goal, Weight = 100m }
            },
            Chapters = new List<Chapter>
            {
                new()
                {
                    ChapterId = NewId.NextGuid(),
                    PathId = pathId,
                    Title = "Chapter 1",
                    OrderIndex = 0,
                    StartDate = chapterStart,
                    EndDate = chapterEnd,
                    EstimatedDays = 7,
                    IsDeleted = false,
                    Lessons = new List<Lesson>
                    {
                        new()
                        {
                            LessonId = NewId.NextGuid(),
                            ChapterId = NewId.NextGuid(),
                            Title = "Lesson 1",
                            OrderIndex = 0,
                            LessonDay = lessonDay,
                            IsDeleted = false
                        }
                    }
                }
            }
        };

        var command = new UpdateMentorLearningPathDraftCommand(
            pathId,
            8,
            subjectId,
            new List<LearningPathGoalRequest> { new(goalId, 100m) },
            ComplexityLevel.Intermediate,
            LanguageSelection.English,
            "Advanced TypeScript Path",
            "Draft description",
            chapterStart,
            chapterEnd,
            new List<ManualChapterRequest>
            {
                new("Chapter 1", chapterStart, chapterEnd, null,
                    new List<ManualLessonRequest>
                    {
                        new("Lesson 1", lessonDay)
                    })
            });

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(new[] { mentor }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new[] { learningPath }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Subjects).Returns(new[] { new Subject { SubjectId = subjectId, Name = "Programming" } }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Goals).Returns(new[] { goal }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SubjectGoals).Returns(new List<SubjectGoal>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathGoals).Returns(new List<LearningPathGoal>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Chapters).Returns(new List<Chapter>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Lessons).Returns(new List<Lesson>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        learningPath.VersionNumber.Should().Be(8);

        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockPublisher.Verify(
            x => x.Publish(
                It.Is<LearningPathDraftVersionUpdatedEvent>(e => e.PathId == pathId && e.CurrentVersion == 8),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithOnlyVersionChanged_SavesAndPublishesUpdate()
    {
        var mentorId = NewId.NextGuid();
        var subjectId = NewId.NextGuid();
        var goalId = NewId.NextGuid();
        var pathId = NewId.NextGuid();
        var chapterStart = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc);
        var chapterEnd = new DateTime(2026, 4, 16, 0, 0, 0, DateTimeKind.Utc);
        var lessonDay = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc);

        var mentor = new User
        {
            UserId = mentorId,
            Username = "mentor_a",
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" }
        };

        var goal = new GoalEntity
        {
            GoalId = goalId,
            Title = "Master TS",
            IsSystemDefined = false,
            IsActive = true,
            CreatedByUserId = mentorId
        };

        var learningPath = new LearningPath
        {
            PathId = pathId,
            UserId = mentorId,
            SubjectId = subjectId,
            Title = "TypeScript Path - ver 5",
            Description = "Draft description",
            StartDate = chapterStart,
            EndDate = chapterEnd,
            Status = LearningPathStatus.Draft.ToString(),
            VersionNumber = 5,
            ComplexityLevel = ComplexityLevel.Intermediate,
            Language = LanguageSelection.English,
            LearningPathGoals = new List<LearningPathGoal>
            {
                new() { PathId = pathId, GoalId = goalId, Goal = goal, Weight = 100m }
            },
            Chapters = new List<Chapter>
            {
                new()
                {
                    ChapterId = NewId.NextGuid(),
                    PathId = pathId,
                    Title = "Chapter 1",
                    OrderIndex = 0,
                    StartDate = chapterStart,
                    EndDate = chapterEnd,
                    EstimatedDays = 7,
                    IsDeleted = false,
                    Lessons = new List<Lesson>
                    {
                        new()
                        {
                            LessonId = NewId.NextGuid(),
                            ChapterId = NewId.NextGuid(),
                            Title = "Lesson 1",
                            OrderIndex = 0,
                            LessonDay = lessonDay,
                            IsDeleted = false
                        }
                    }
                }
            }
        };

        var command = new UpdateMentorLearningPathDraftCommand(
            pathId,
            9,
            subjectId,
            new List<LearningPathGoalRequest> { new(goalId, 100m) },
            ComplexityLevel.Intermediate,
            LanguageSelection.English,
            "TypeScript Path",
            "Draft description",
            chapterStart,
            chapterEnd,
            new List<ManualChapterRequest>
            {
                new("Chapter 1", chapterStart, chapterEnd, null,
                    new List<ManualLessonRequest>
                    {
                        new("Lesson 1", lessonDay)
                    })
            });

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(new[] { mentor }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new[] { learningPath }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Subjects).Returns(new[] { new Subject { SubjectId = subjectId, Name = "Programming" } }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Goals).Returns(new[] { goal }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SubjectGoals).Returns(new List<SubjectGoal>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathGoals).Returns(new List<LearningPathGoal>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Chapters).Returns(new List<Chapter>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Lessons).Returns(new List<Lesson>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        learningPath.VersionNumber.Should().Be(9);
        learningPath.Title.Should().Be("TypeScript Path - ver 9");

        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockPublisher.Verify(
            x => x.Publish(
                It.Is<LearningPathDraftVersionUpdatedEvent>(e => e.PathId == pathId && e.CurrentVersion == 9),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_NoMeaningfulChanges_ButDifferentTimeComponent_DoesNotSaveOrPublish()
    {
        var mentorId = NewId.NextGuid();
        var subjectId = NewId.NextGuid();
        var goalId = NewId.NextGuid();
        var pathId = NewId.NextGuid();
        var chapterStart = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc);
        var chapterEnd = new DateTime(2026, 4, 16, 0, 0, 0, DateTimeKind.Utc);
        var lessonDay = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc);

        var mentor = new User
        {
            UserId = mentorId,
            Username = "mentor_a",
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" }
        };

        var goal = new GoalEntity
        {
            GoalId = goalId,
            Title = "Master TS",
            IsSystemDefined = false,
            IsActive = true,
            CreatedByUserId = mentorId
        };

        var learningPath = new LearningPath
        {
            PathId = pathId,
            UserId = mentorId,
            SubjectId = subjectId,
            Title = "TypeScript Path - ver 5",
            Description = "Draft description",
            StartDate = chapterStart,
            EndDate = chapterEnd,
            Status = LearningPathStatus.Draft.ToString(),
            VersionNumber = 5,
            ComplexityLevel = ComplexityLevel.Intermediate,
            Language = LanguageSelection.English,
            LearningPathGoals = new List<LearningPathGoal>
            {
                new() { PathId = pathId, GoalId = goalId, Goal = goal, Weight = 100m }
            },
            Chapters = new List<Chapter>
            {
                new()
                {
                    ChapterId = NewId.NextGuid(),
                    PathId = pathId,
                    Title = "Chapter 1",
                    OrderIndex = 0,
                    StartDate = chapterStart,
                    EndDate = chapterEnd,
                    EstimatedDays = 7,
                    IsDeleted = false,
                    Lessons = new List<Lesson>
                    {
                        new()
                        {
                            LessonId = NewId.NextGuid(),
                            ChapterId = NewId.NextGuid(),
                            Title = "Lesson 1",
                            OrderIndex = 0,
                            LessonDay = lessonDay,
                            IsDeleted = false
                        }
                    }
                }
            }
        };

        var command = new UpdateMentorLearningPathDraftCommand(
            pathId,
            5,
            subjectId,
            new List<LearningPathGoalRequest> { new(goalId, 100m) },
            ComplexityLevel.Intermediate,
            LanguageSelection.English,
            "TypeScript Path",
            "Draft description",
            new DateTime(2026, 4, 10, 7, 30, 0, DateTimeKind.Local),
            new DateTime(2026, 4, 16, 18, 15, 0, DateTimeKind.Local),
            new List<ManualChapterRequest>
            {
                new(
                    "Chapter 1",
                    new DateTime(2026, 4, 10, 8, 0, 0, DateTimeKind.Local),
                    new DateTime(2026, 4, 16, 20, 0, 0, DateTimeKind.Local),
                    null,
                    new List<ManualLessonRequest>
                    {
                        new("Lesson 1", new DateTime(2026, 4, 10, 11, 45, 0, DateTimeKind.Local))
                    })
            });

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(new[] { mentor }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new[] { learningPath }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Subjects).Returns(new[] { new Subject { SubjectId = subjectId, Name = "Programming" } }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Goals).Returns(new[] { goal }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SubjectGoals).Returns(new List<SubjectGoal>().BuildMockDbSet().Object);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        learningPath.VersionNumber.Should().Be(5);

        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockPublisher.Verify(
            x => x.Publish(It.IsAny<LearningPathDraftVersionUpdatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithOnlyEmptyNestedNodes_DoesNotSaveOrPublish()
    {
        var mentorId = NewId.NextGuid();
        var subjectId = NewId.NextGuid();
        var goalId = NewId.NextGuid();
        var pathId = NewId.NextGuid();
        var startDate = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2026, 4, 16, 0, 0, 0, DateTimeKind.Utc);

        var mentor = new User
        {
            UserId = mentorId,
            Username = "mentor_a",
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" }
        };

        var goal = new GoalEntity
        {
            GoalId = goalId,
            Title = "Master TS",
            IsSystemDefined = false,
            IsActive = true,
            CreatedByUserId = mentorId
        };

        var learningPath = new LearningPath
        {
            PathId = pathId,
            UserId = mentorId,
            SubjectId = subjectId,
            Title = "TypeScript Path - ver 5",
            Description = "Draft description",
            StartDate = startDate,
            EndDate = endDate,
            Status = LearningPathStatus.Draft.ToString(),
            VersionNumber = 5,
            ComplexityLevel = ComplexityLevel.Intermediate,
            Language = LanguageSelection.English,
            LearningPathGoals = new List<LearningPathGoal>
            {
                new() { PathId = pathId, GoalId = goalId, Goal = goal, Weight = 100m }
            },
            Chapters = new List<Chapter>()
        };

        var command = new UpdateMentorLearningPathDraftCommand(
            pathId,
            5,
            subjectId,
            new List<LearningPathGoalRequest> { new(goalId, 100m) },
            ComplexityLevel.Intermediate,
            LanguageSelection.English,
            "TypeScript Path",
            "Draft description",
            startDate,
            endDate,
            new List<ManualChapterRequest>
            {
                new(
                    string.Empty,
                    null,
                    null,
                    null,
                    new List<ManualLessonRequest>
                    {
                        new(string.Empty, default, new List<ManualQuizRequest>
                        {
                            new(string.Empty, null, null, new List<ManualQuestionRequest>
                            {
                                new(string.Empty, QuestionType.SingleChoice, null, null, 1)
                            })
                        })
                    },
                    new List<ManualTaskRequest>
                    {
                        new(string.Empty, null, TaskType.Practice, null, null, null)
                    })
            });

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(new[] { mentor }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new[] { learningPath }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Subjects).Returns(new[] { new Subject { SubjectId = subjectId, Name = "Programming" } }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Goals).Returns(new[] { goal }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SubjectGoals).Returns(new List<SubjectGoal>().BuildMockDbSet().Object);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        learningPath.VersionNumber.Should().Be(5);
        result.Value!.ChapterDtos.Should().BeEmpty();

        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mockPublisher.Verify(
            x => x.Publish(It.IsAny<LearningPathDraftVersionUpdatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithLessonContentChanged_SavesUpdatedContent()
    {
        var mentorId = NewId.NextGuid();
        var subjectId = NewId.NextGuid();
        var goalId = NewId.NextGuid();
        var pathId = NewId.NextGuid();
        var chapterId = NewId.NextGuid();
        var chapterStart = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc);
        var chapterEnd = new DateTime(2026, 4, 16, 0, 0, 0, DateTimeKind.Utc);
        var lessonDay = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc);

        var mentor = new User
        {
            UserId = mentorId,
            Username = "mentor_a",
            Role = new Role { RoleId = NewId.NextGuid(), RoleName = "Mentor" }
        };

        var goal = new GoalEntity
        {
            GoalId = goalId,
            Title = "Master TS",
            IsSystemDefined = false,
            IsActive = true,
            CreatedByUserId = mentorId
        };

        var learningPath = new LearningPath
        {
            PathId = pathId,
            UserId = mentorId,
            SubjectId = subjectId,
            Title = "TypeScript Path - ver 5",
            Description = "Draft description",
            StartDate = chapterStart,
            EndDate = chapterEnd,
            Status = LearningPathStatus.Draft.ToString(),
            VersionNumber = 5,
            ComplexityLevel = ComplexityLevel.Intermediate,
            Language = LanguageSelection.English,
            LearningPathGoals = new List<LearningPathGoal>
            {
                new() { PathId = pathId, GoalId = goalId, Goal = goal, Weight = 100m }
            },
            Chapters = new List<Chapter>
            {
                new()
                {
                    ChapterId = chapterId,
                    PathId = pathId,
                    Title = "Chapter 1",
                    OrderIndex = 0,
                    StartDate = chapterStart,
                    EndDate = chapterEnd,
                    EstimatedDays = 7,
                    IsDeleted = false,
                    Lessons = new List<Lesson>
                    {
                        new()
                        {
                            LessonId = NewId.NextGuid(),
                            ChapterId = chapterId,
                            Title = "Lesson 1",
                            Content = "Old content",
                            OrderIndex = 0,
                            LessonDay = lessonDay,
                            IsDeleted = false,
                            UpdatedAt = chapterStart
                        }
                    }
                }
            }
        };

        var command = new UpdateMentorLearningPathDraftCommand(
            pathId,
            5,
            subjectId,
            new List<LearningPathGoalRequest> { new(goalId, 100m) },
            ComplexityLevel.Intermediate,
            LanguageSelection.English,
            "TypeScript Path",
            "Draft description",
            chapterStart,
            chapterEnd,
            new List<ManualChapterRequest>
            {
                new("Chapter 1", chapterStart, chapterEnd, null,
                    new List<ManualLessonRequest>
                    {
                        new("Lesson 1", lessonDay, null, "New content")
                    })
            });

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(mentorId);
        _mockContext.Setup(x => x.Users).Returns(new[] { mentor }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPaths).Returns(new[] { learningPath }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Subjects).Returns(new[] { new Subject { SubjectId = subjectId, Name = "Programming" } }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Goals).Returns(new[] { goal }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SubjectGoals).Returns(new List<SubjectGoal>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.LearningPathGoals).Returns(new List<LearningPathGoal>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Chapters).Returns(new List<Chapter>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.Lessons).Returns(new List<Lesson>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        learningPath.Chapters.First().Lessons.First().Content.Should().Be("New content");
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
