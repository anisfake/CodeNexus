using CodeNexus.Application.Features.Users.Commands.CreateMentorAccount;
using FluentValidation.TestHelper;

namespace CodeNexus.UnitTests.Features.Users;

public class CreateMentorAccountCommandValidatorTests
{
    private readonly CreateMentorAccountCommandValidator _validator = new();

    [Theory]
    [InlineData("")]
    [InlineData("invalid-email")]
    [InlineData("test@")]
    public void Validate_InvalidEmail_ShouldHaveError(string email)
    {
        var result = _validator.TestValidate(BuildValidCommand() with { Email = email });
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("name with space")]
    [InlineData("name!")]
    public void Validate_InvalidUsername_ShouldHaveError(string username)
    {
        var result = _validator.TestValidate(BuildValidCommand() with { Username = username });
        result.ShouldHaveValidationErrorFor(x => x.Username);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Admin")]
    [InlineData("SuperUser")]
    public void Validate_InvalidRole_ShouldHaveError(string role)
    {
        var result = _validator.TestValidate(BuildValidCommand() with { Role = role });
        result.ShouldHaveValidationErrorFor(x => x.Role);
    }

    [Theory]
    [InlineData("Mentor")]
    [InlineData("Student")]
    public void Validate_ValidRole_ShouldNotHaveError(string role)
    {
        var result = _validator.TestValidate(BuildValidCommand() with { Role = role });
        result.ShouldNotHaveValidationErrorFor(x => x.Role);
    }

    [Fact]
    public void Validate_FutureBirthDate_ShouldHaveError()
    {
        var result = _validator.TestValidate(BuildValidCommand() with { DateOfBirth = DateTime.UtcNow.Date.AddDays(1) });
        result.ShouldHaveValidationErrorFor(x => x.DateOfBirth);
    }

    [Fact]
    public void Validate_NullUsername_ShouldNotHaveError()
    {
        var result = _validator.TestValidate(BuildValidCommand() with { Username = null });
        result.ShouldNotHaveValidationErrorFor(x => x.Username);
    }

    [Fact]
    public void Validate_ValidCommand_ShouldNotHaveAnyError()
    {
        var result = _validator.TestValidate(BuildValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    private static CreateMentorAccountCommand BuildValidCommand() =>
        new("user@test.com", "user_001", "First", "Last", "Bio", "0123456789", "HCM", new DateTime(1995, 1, 1), "Mentor", true);
}
