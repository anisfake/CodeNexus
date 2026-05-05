using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Application.Features.LearningPathSkeleton.Commands.UpdateStudentLearningPath;
using FluentAssertions;
using FluentValidation.TestHelper;
using MassTransit;

namespace CodeNexus.UnitTests.Features.LearningPaths.Validators;

public class UpdateStudentLearningPathCommandValidatorTests
{
    private readonly UpdateStudentLearningPathCommandValidator _validator = new();

    private static UpdateStudentLearningPathCommand BuildValidCommand()
    {
        return new UpdateStudentLearningPathCommand(
            NewId.NextGuid(),
            new List<StudentChapterRequest>
            {
                new("Chapter 1", null, null, null,
                    new List<StudentLessonRequest>
                    {
                        new("Lesson 1", new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc))
                    })
            });
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
    public void Validate_NullChapters_FailsWithChaptersRequired()
    {
        var command = BuildValidCommand() with { Chapters = null! };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Chapters).WithErrorCode("CHAPTERS_REQUIRED");
    }

    [Fact]
    public void Validate_EmptyChapters_FailsWithChaptersRequired()
    {
        var command = BuildValidCommand() with { Chapters = new List<StudentChapterRequest>() };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Chapters).WithErrorCode("CHAPTERS_REQUIRED");
    }

    [Fact]
    public void Validate_ChapterWithEmptyTitle_FailsWithChapterTitleRequired()
    {
        var command = BuildValidCommand() with
        {
            Chapters = new List<StudentChapterRequest>
            {
                new("", null, null, null,
                    new List<StudentLessonRequest>
                    {
                        new("Lesson 1", new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc))
                    })
            }
        };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor("Chapters[0].Title").WithErrorCode("CHAPTER_TITLE_REQUIRED");
    }

    [Fact]
    public void Validate_ChapterWithNoLessons_FailsWithLessonsRequired()
    {
        var command = BuildValidCommand() with
        {
            Chapters = new List<StudentChapterRequest>
            {
                new("Chapter 1", null, null, null, new List<StudentLessonRequest>())
            }
        };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor("Chapters[0].Lessons").WithErrorCode("LESSONS_REQUIRED");
    }

    [Fact]
    public void Validate_LessonWithEmptyTitle_FailsWithLessonTitleRequired()
    {
        var command = BuildValidCommand() with
        {
            Chapters = new List<StudentChapterRequest>
            {
                new("Chapter 1", null, null, null,
                    new List<StudentLessonRequest>
                    {
                        new("", new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc))
                    })
            }
        };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor("Chapters[0].Lessons[0].Title").WithErrorCode("LESSON_TITLE_REQUIRED");
    }
}
