using CodeNexus.Application.Features.Auth.Commands.VerifyOtp;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace CodeNexus.UnitTests.Features.Auth.Validators;

public class VerifyOtpCommandValidatorTests
{
    private readonly VerifyOtpCommandValidator _validator;

    public VerifyOtpCommandValidatorTests()
    {
        _validator = new VerifyOtpCommandValidator();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Email_WhenEmpty_ShouldHaveError(string? email)
    {
        var command = new VerifyOtpCommand(email!, "123456");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("invalid@")]
    [InlineData("@invalid.com")]
    public void Email_WhenInvalidFormat_ShouldHaveError(string email)
    {
        var command = new VerifyOtpCommand(email, "123456");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorCode("INVALID_EMAIL_FORMAT");
    }

    [Fact]
    public void Email_WhenValid_ShouldNotHaveError()
    {
        var command = new VerifyOtpCommand("valid@test.com", "123456");
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Otp_WhenEmpty_ShouldHaveError(string? otp)
    {
        var command = new VerifyOtpCommand("test@test.com", otp!);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Otp);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("abcdef")]
    [InlineData("12345a")]
    public void Otp_WhenInvalidFormat_ShouldHaveError(string otp)
    {
        var command = new VerifyOtpCommand("test@test.com", otp);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Otp)
            .WithErrorCode("INVALID_OTP");
    }

    [Theory]
    [InlineData("123456")]
    [InlineData("000000")]
    [InlineData("999999")]
    public void Otp_WhenValid_ShouldNotHaveError(string otp)
    {
        var command = new VerifyOtpCommand("test@test.com", otp);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Otp);
    }
}
