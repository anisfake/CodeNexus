using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Application.Features.LearningPathSkeleton.Commands.PublishMentorLearningPath;
using CodeNexus.Domain.Enums;
using FluentAssertions;
using FluentValidation.TestHelper;
using MassTransit;

namespace CodeNexus.UnitTests.Features.LearningPaths;

public class PublishMentorLearningPathCommandValidatorTests
{
    private readonly PublishMentorLearningPathCommandValidator _validator = new();

    private static ManualChapterRequest BuildChapterWithContent(string chapterTitle = "Chapter 1", string lessonContent = "Lesson content here")
    {
        return new ManualChapterRequest(
            chapterTitle,
            null, null, 7,
            new List<ManualLessonRequest>
            {
                new("Lesson 1", new DateTime(2026, 1, 7, 0, 0, 0, DateTimeKind.Utc), null, lessonContent)
            });
    }

    private static PublishMentorLearningPathCommand BuildValidCommand(
        List<ManualChapterRequest>? chapters = null,
        bool increaseVersion = false)
    {
        return new PublishMentorLearningPathCommand(
            NewId.NextGuid(),
            increaseVersion,
            increaseVersion ? DraftVersionUpdateType.Minor : null,
            NewId.NextGuid(),
            new List<LearningPathGoalRequest> { new(NewId.NextGuid(), 100) },
            ComplexityLevel.Beginner,
            LanguageSelection.VietNamese,
            "Test Path",
            null,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            chapters ?? new List<ManualChapterRequest> { BuildChapterWithContent() });
    }

    [Fact]
    public void Validate_ValidCommand_PassesValidation()
    {
        var command = BuildValidCommand();
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyPathId_FailsWithPathRequired()
    {
        var command = BuildValidCommand() with { PathId = Guid.Empty };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.PathId).WithErrorCode("PATH_REQUIRED");
    }

    [Fact]
    public void Validate_EmptyTitle_FailsWithInvalidTitle()
    {
        var command = BuildValidCommand() with { Title = "" };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Title).WithErrorCode("INVALID_TITLE");
    }

    [Fact]
    public void Validate_NoChapters_FailsWithChaptersRequired()
    {
        var command = BuildValidCommand(chapters: new List<ManualChapterRequest>());
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Chapters).WithErrorCode("CHAPTERS_REQUIRED");
    }

    [Fact]
    public void Validate_LessonWithEmptyContent_FailsWithContentIncomplete()
    {
        var chapterWithNoLessonContent = new ManualChapterRequest(
            "Chapter 1", null, null, 7,
            new List<ManualLessonRequest>
            {
                new("Lesson 1", new DateTime(2026, 1, 7, 0, 0, 0, DateTimeKind.Utc), null, "")
            });
        var command = BuildValidCommand(chapters: new List<ManualChapterRequest> { chapterWithNoLessonContent });
        var result = _validator.TestValidate(command);
        result.Errors.Should().Contain(e => e.ErrorCode == "CONTENT_INCOMPLETE");
    }

    [Fact]
    public void Validate_LessonWithNullContent_FailsWithContentIncomplete()
    {
        var chapterWithNullContent = new ManualChapterRequest(
            "Chapter 1", null, null, 7,
            new List<ManualLessonRequest>
            {
                new("Lesson 1", new DateTime(2026, 1, 7, 0, 0, 0, DateTimeKind.Utc), null, null)
            });
        var command = BuildValidCommand(chapters: new List<ManualChapterRequest> { chapterWithNullContent });
        var result = _validator.TestValidate(command);
        result.Errors.Should().Contain(e => e.ErrorCode == "CONTENT_INCOMPLETE");
    }

    [Fact]
    public void Validate_IncreaseVersionWithoutVersionType_FailsWithVersionUpdateTypeRequired()
    {
        var command = new PublishMentorLearningPathCommand(
            NewId.NextGuid(), true, null,
            NewId.NextGuid(),
            new List<LearningPathGoalRequest> { new(NewId.NextGuid(), 100) },
            ComplexityLevel.Beginner, LanguageSelection.VietNamese,
            "Test Path", null,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new List<ManualChapterRequest> { BuildChapterWithContent() });

        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.VersionUpdateType).WithErrorCode("VERSION_UPDATE_TYPE_REQUIRED");
    }

    [Fact]
    public void Validate_NoGoals_FailsWithInvalidGoals()
    {
        var command = new PublishMentorLearningPathCommand(
            NewId.NextGuid(), false, null,
            NewId.NextGuid(),
            new List<LearningPathGoalRequest>(),
            ComplexityLevel.Beginner, LanguageSelection.VietNamese,
            "Test Path", null,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new List<ManualChapterRequest> { BuildChapterWithContent() });

        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Goals).WithErrorCode("INVALID_GOALS");
    }

    [Fact]
    public void Validate_StartDateAfterEndDate_FailsWithInvalidDateRange()
    {
        var command = new PublishMentorLearningPathCommand(
            NewId.NextGuid(), false, null,
            NewId.NextGuid(),
            new List<LearningPathGoalRequest> { new(NewId.NextGuid(), 100) },
            ComplexityLevel.Beginner, LanguageSelection.VietNamese,
            "Test Path", null,
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new List<ManualChapterRequest> { BuildChapterWithContent() });

        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.StartDate).WithErrorCode("INVALID_DATE_RANGE");
    }
}
