using CodeNexus.Application.Features.Auth.Commands.ResetPassword;
using FluentValidation.TestHelper;

namespace CodeNexus.UnitTests.Features.Auth.Validators;

public class ResetPasswordCommandValidatorTests
{
    private readonly ResetPasswordCommandValidator _validator;

    public ResetPasswordCommandValidatorTests()
    {
        _validator = new ResetPasswordCommandValidator();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void ResetToken_WhenEmpty_ShouldHaveError(string? token)
    {
        var command = new ResetPasswordCommand(token!, "Password123");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ResetToken)
            .WithErrorCode("INVALID_TOKEN");
    }

    [Fact]
    public void ResetToken_WhenValid_ShouldNotHaveError()
    {
        var command = new ResetPasswordCommand("validToken123", "Password123");
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.ResetToken);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void NewPassword_WhenEmpty_ShouldHaveError(string? password)
    {
        var command = new ResetPasswordCommand("validToken", password!);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.NewPassword);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("nouppercase1")]
    [InlineData("NOLOWERCASE1")]
    [InlineData("NoNumbers")]
    [InlineData("Pass1")]
    public void NewPassword_WhenInvalidFormat_ShouldHaveError(string password)
    {
        var command = new ResetPasswordCommand("validToken", password);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorCode("INVALID_PASSWORD");
    }

    [Theory]
    [InlineData("Password1")]
    [InlineData("ValidPass123")]
    [InlineData("MyP@ssw0rd")]
    public void NewPassword_WhenValid_ShouldNotHaveError(string password)
    {
        var command = new ResetPasswordCommand("validToken", password);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.NewPassword);
    }
}
