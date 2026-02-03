using CodeNexus.Application.Features.Profile.Commands.ChangePassword;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace CodeNexus.UnitTests.Features.Profile;

public class ChangePasswordCommandValidatorTests
{
    private readonly ChangePasswordCommandValidator _validator;

    public ChangePasswordCommandValidatorTests()
    {
        _validator = new ChangePasswordCommandValidator();
    }

    [Fact]
    public void Validate_WhenUserIdIsEmpty_ShouldHaveValidationError()
    {
        // Arrange
        var command = new ChangePasswordCommand(Guid.Empty, "CurrentPass123", "NewPass456");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorCode("INVALID_USER_ID");
    }

    [Fact]
    public void Validate_WhenCurrentPasswordIsEmpty_ShouldHaveValidationError()
    {
        // Arrange
        var command = new ChangePasswordCommand(Guid.NewGuid(), "", "NewPass456");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.CurrentPassword)
            .WithErrorCode("INVALID_CURRENT_PASSWORD");
    }

    [Fact]
    public void Validate_WhenNewPasswordIsEmpty_ShouldHaveValidationError()
    {
        // Arrange
        var command = new ChangePasswordCommand(Guid.NewGuid(), "CurrentPass123", "");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorCode("INVALID_NEW_PASSWORD");
    }

    [Fact]
    public void Validate_WhenNewPasswordTooShort_ShouldHaveValidationError()
    {
        // Arrange
        var command = new ChangePasswordCommand(Guid.NewGuid(), "CurrentPass123", "Short1");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorCode("INVALID_NEW_PASSWORD");
    }

    [Fact]
    public void Validate_WhenNewPasswordNoUppercase_ShouldHaveValidationError()
    {
        // Arrange
        var command = new ChangePasswordCommand(Guid.NewGuid(), "CurrentPass123", "newpass123");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorCode("INVALID_NEW_PASSWORD");
    }

    [Fact]
    public void Validate_WhenNewPasswordNoLowercase_ShouldHaveValidationError()
    {
        // Arrange
        var command = new ChangePasswordCommand(Guid.NewGuid(), "CurrentPass123", "NEWPASS123");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorCode("INVALID_NEW_PASSWORD");
    }

    [Fact]
    public void Validate_WhenNewPasswordNoNumber_ShouldHaveValidationError()
    {
        // Arrange
        var command = new ChangePasswordCommand(Guid.NewGuid(), "CurrentPass123", "NewPassword");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorCode("INVALID_NEW_PASSWORD");
    }

    [Fact]
    public void Validate_WhenNewPasswordSameAsCurrentPassword_ShouldHaveValidationError()
    {
        // Arrange
        var command = new ChangePasswordCommand(Guid.NewGuid(), "SamePass123", "SamePass123");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x)
            .WithErrorCode("PASSWORD_SAME_AS_CURRENT")
            .WithErrorMessage("New password must be different from current password");
    }

    [Fact]
    public void Validate_WhenAllFieldsValid_ShouldNotHaveValidationErrors()
    {
        // Arrange
        var command = new ChangePasswordCommand(Guid.NewGuid(), "CurrentPass123", "NewPass456");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenNewPasswordMeetsAllRequirements_ShouldPass()
    {
        // Arrange
        var command = new ChangePasswordCommand(Guid.NewGuid(), "OldPassword1", "NewPassword123");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.NewPassword);
    }

    [Theory]
    [InlineData("Pass1")] // Too short
    [InlineData("password123")] // No uppercase
    [InlineData("PASSWORD123")] // No lowercase
    [InlineData("PasswordABC")] // No number
    [InlineData("short1A")] // Too short
    public void Validate_WhenNewPasswordInvalid_ShouldHaveValidationError(string invalidPassword)
    {
        // Arrange
        var command = new ChangePasswordCommand(Guid.NewGuid(), "CurrentPass123", invalidPassword);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorCode("INVALID_NEW_PASSWORD");
    }

    [Theory]
    [InlineData("ValidPass123")]
    [InlineData("NewPassword456")]
    [InlineData("Secure123Password")]
    [InlineData("MyNewP@ss123")]
    public void Validate_WhenNewPasswordValid_ShouldNotHaveValidationError(string validPassword)
    {
        // Arrange
        var command = new ChangePasswordCommand(Guid.NewGuid(), "CurrentPass123", validPassword);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.NewPassword);
    }
}
