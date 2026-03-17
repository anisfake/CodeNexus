using CodeNexus.Application.Features.LearningPathShares.Commands.RejectLearningPathShare;
using FluentValidation.TestHelper;
using MassTransit;

namespace CodeNexus.UnitTests.Features.LearningPathShares.Validators;

public class RejectLearningPathShareCommandValidatorTests
{
    private readonly RejectLearningPathShareCommandValidator _validator;

    public RejectLearningPathShareCommandValidatorTests()
    {
        _validator = new RejectLearningPathShareCommandValidator();
    }

    [Fact]
    public void Validate_EmptyShareId_ShouldHaveValidationError()
    {
        var command = new RejectLearningPathShareCommand(Guid.Empty);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.ShareId)
            .WithErrorCode("SHARE_ID_REQUIRED");
    }

    [Fact]
    public void Validate_ValidShareId_ShouldNotHaveValidationError()
    {
        var command = new RejectLearningPathShareCommand(NewId.NextGuid());

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveValidationErrorFor(x => x.ShareId);
    }
}
