using CodeNexus.Application.Features.Tasks.Commands.GenerateSingleTask;
using CodeNexus.Domain.Enums;
using FluentValidation.TestHelper;
using MassTransit;
using Xunit;

namespace CodeNexus.UnitTests.Features.Tasks.Validators;

public class GenerateSingleTaskCommandValidatorTests
{
    private readonly GenerateSingleTaskCommandValidator _validator;

    public GenerateSingleTaskCommandValidatorTests()
    {
        _validator = new GenerateSingleTaskCommandValidator();
    }

    [Fact]
    public void Validate_PracticeTaskType_ShouldNotHaveError()
    {
        // Arrange
        var command = new GenerateSingleTaskCommand(NewId.NextGuid(), "Valid title", TaskType.Practice);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.TaskType);
    }

    [Fact]
    public void Validate_QuizzTaskType_ShouldHaveError()
    {
        // Arrange
        var command = new GenerateSingleTaskCommand(NewId.NextGuid(), "Quiz title", TaskType.Quizz);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.TaskType)
            .WithErrorCode("TASK_TYPE_INVALID");
    }
}
