using CodeNexus.Application.Features.LearningPathShares.Commands.ApplyLearningPathShareUpdate;
using CodeNexus.Application.Features.LearningPathShares.DTOs;
using FluentValidation.TestHelper;
using MassTransit;

namespace CodeNexus.UnitTests.Features.LearningPathShares.Validators;

public class ApplyLearningPathShareUpdateCommandValidatorTests
{
    private readonly ApplyLearningPathShareUpdateCommandValidator _validator;

    public ApplyLearningPathShareUpdateCommandValidatorTests()
    {
        _validator = new ApplyLearningPathShareUpdateCommandValidator();
    }

    [Fact]
    public void Validate_EmptyShareId_ShouldHaveValidationError()
    {
        var command = new ApplyLearningPathShareUpdateCommand(Guid.Empty, LearningPathShareUpdateAction.CreateNewFromLatest);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.ShareId)
            .WithErrorCode("SHARE_ID_REQUIRED");
    }

    [Fact]
    public void Validate_ValidCommand_ShouldNotHaveValidationError()
    {
        var command = new ApplyLearningPathShareUpdateCommand(NewId.NextGuid(), LearningPathShareUpdateAction.UpdateCurrentToLatest);

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.ShareId);
    }
}
