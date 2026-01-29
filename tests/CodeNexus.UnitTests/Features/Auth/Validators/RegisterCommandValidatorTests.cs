using CodeNexus.Application.Features.Auth.Commands.Register;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace CodeNexus.UnitTests.Features.Auth.Validators;

public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator;

    public RegisterCommandValidatorTests()
    {
        _validator = new RegisterCommandValidator();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Email_WhenEmpty_ShouldHaveError(string? email)
    {
        var command = new RegisterCommand(email!, "username", "John", "Doe", "Password123");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("invalid@")]
    [InlineData("@invalid.com")]
    [InlineData("invalid.com")]
    public void Email_WhenInvalidFormat_ShouldHaveError(string email)
    {
        var command = new RegisterCommand(email, "username", "John", "Doe", "Password123");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorCode("INVALID_EMAIL_FORMAT");
    }

    [Fact]
    public void Email_WhenValid_ShouldNotHaveError()
    {
        var command = new RegisterCommand("valid@test.com", "username", "John", "Doe", "Password123");
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Username_WhenEmpty_ShouldHaveError(string? username)
    {
        var command = new RegisterCommand("test@test.com", username!, "John", "Doe", "Password123");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Username);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("a")]
    public void Username_WhenTooShort_ShouldHaveError(string username)
    {
        var command = new RegisterCommand("test@test.com", username, "John", "Doe", "Password123");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Username)
            .WithErrorCode("INVALID_USERNAME");
    }

    [Fact]
    public void Username_WhenTooLong_ShouldHaveError()
    {
        var longUsername = new string('a', 51);
        var command = new RegisterCommand("test@test.com", longUsername, "John", "Doe", "Password123");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Username)
            .WithErrorCode("INVALID_USERNAME");
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("validuser")]
    [InlineData("user123")]
    public void Username_WhenValid_ShouldNotHaveError(string username)
    {
        var command = new RegisterCommand("test@test.com", username, "John", "Doe", "Password123");
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Username);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Password_WhenEmpty_ShouldHaveError(string? password)
    {
        var command = new RegisterCommand("test@test.com", "username", "John", "Doe", password!);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("nouppercase1")]
    [InlineData("NOLOWERCASE1")]
    [InlineData("NoNumbers")]
    [InlineData("Pass1")]
    public void Password_WhenInvalidFormat_ShouldHaveError(string password)
    {
        var command = new RegisterCommand("test@test.com", "username", "John", "Doe", password);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorCode("INVALID_PASSWORD");
    }

    [Theory]
    [InlineData("Password1")]
    [InlineData("ValidPass123")]
    [InlineData("MyP@ssw0rd")]
    public void Password_WhenValid_ShouldNotHaveError(string password)
    {
        var command = new RegisterCommand("test@test.com", "username", "John", "Doe", password);
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }
}
