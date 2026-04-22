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
        var command = BuildValidCommand() with { Email = email };
        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("name with space")]
    [InlineData("name!")]
    public void Validate_InvalidUsername_ShouldHaveError(string username)
    {
        var command = BuildValidCommand() with { Username = username };
        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Username);
    }

    [Fact]
    public void Validate_FutureBirthDate_ShouldHaveError()
    {
        var command = BuildValidCommand() with { DateOfBirth = DateTime.UtcNow.Date.AddDays(1) };
        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.DateOfBirth);
    }

    [Fact]
    public void Validate_ValidCommand_ShouldNotHaveAnyError()
    {
        var result = _validator.TestValidate(BuildValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    private static CreateMentorAccountCommand BuildValidCommand()
    {
        return new CreateMentorAccountCommand(
            "mentor@test.com",
            "mentor_001",
            "Mentor",
            "User",
            "Bio",
            "0123456789",
            "HCM",
            new DateTime(1995, 1, 1),
            true);
    }
}
