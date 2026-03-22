using CodeNexus.Application.Features.Lessons.Queries.GetLessonReadStatus;
using FluentValidation.TestHelper;

namespace CodeNexus.UnitTests.Features.Lessons;

public class GetLessonReadStatusQueryValidatorTests
{
    private readonly GetLessonReadStatusQueryValidator _validator;

    public GetLessonReadStatusQueryValidatorTests()
    {
        _validator = new GetLessonReadStatusQueryValidator();
    }

    [Fact]
    public void Validate_ValidLessonId_ShouldNotHaveErrors()
    {
        var query = new GetLessonReadStatusQuery(Guid.NewGuid());

        var result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyLessonId_ShouldHaveError()
    {
        var query = new GetLessonReadStatusQuery(Guid.Empty);

        var result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(x => x.LessonId)
            .WithErrorCode("LESSON_ID_REQUIRED");
    }
}
