using CodeNexus.Application.Features.Lessons.Commands.MarkLessonContentRead;
using FluentValidation.TestHelper;

namespace CodeNexus.UnitTests.Features.Lessons;

public class MarkLessonContentReadCommandValidatorTests
{
    private readonly MarkLessonContentReadCommandValidator _validator;

    public MarkLessonContentReadCommandValidatorTests()
    {
        _validator = new MarkLessonContentReadCommandValidator();
    }

    [Fact]
    public void Validate_ValidLessonId_ShouldNotHaveErrors()
    {
        var command = new MarkLessonContentReadCommand(Guid.NewGuid());

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyLessonId_ShouldHaveError()
    {
        var command = new MarkLessonContentReadCommand(Guid.Empty);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.LessonId)
            .WithErrorCode("LESSON_ID_REQUIRED");
    }
}
