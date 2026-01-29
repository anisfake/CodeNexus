using CodeNexus.Application.Features.Auth.Commands.ResendOtp;
using FluentValidation.TestHelper;

namespace CodeNexus.UnitTests.Features.Auth.Validators;

public class ResendOtpCommandValidatorTests
{
    private readonly ResendOtpCommandValidator _validator;

    public ResendOtpCommandValidatorTests()
    {
        _validator = new ResendOtpCommandValidator();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Email_WhenEmpty_ShouldHaveError(string? email)
    {
        var command = new ResendOtpCommand(email!);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("invalid@")]
    [InlineData("@invalid.com")]
    public void Email_WhenInvalidFormat_ShouldHaveError(string email)
    {
        var command = new ResendOtpCommand(email);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorCode("INVALID_EMAIL_FORMAT");
    }

    [Theory]
    [InlineData("valid@test.com")]
    [InlineData("user@domain.org")]
    public void Email_WhenValid_ShouldNotHaveError(string email)
    {
        var command = new ResendOtpCommand(email);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }
}
