using CodeNexus.Application.Features.Auth.Commands.Register;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using System.Text.RegularExpressions;

namespace CodeNexus.UnitTests.Features.Auth;

public class RegisterCommandValidatorPropertyTests
{
    private readonly RegisterCommandValidator _validator = new();
    private const string ValidEmailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
    private const string ValidPasswordPattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$";

    [Property(MaxTest = 100)]
    public Property InvalidEmailFormat_ShouldBeRejected()
    {
        return Prop.ForAll(InvalidEmailGenerator(), invalidEmail =>
        {
            // Arrange
            var command = new RegisterCommand(invalidEmail, "validuser", "ValidPass1");

            // Act
            var result = _validator.Validate(command);

            // Assert
            return (!result.IsValid &&
                    result.Errors.Any(e => e.PropertyName == "Email"))
                .Label($"Invalid email '{invalidEmail}' should be rejected");
        });
    }

    [Property(MaxTest = 100)]
    public Property InvalidPassword_ShouldBeRejected()
    {
        return Prop.ForAll(InvalidPasswordGenerator(), invalidPassword =>
        {
            // Arrange
            var command = new RegisterCommand("valid@example.com", "validuser", invalidPassword);

            // Act
            var result = _validator.Validate(command);

            // Assert
            return (!result.IsValid &&
                    result.Errors.Any(e => e.PropertyName == "Password"))
                .Label($"Invalid password '{invalidPassword}' should be rejected");
        });
    }

    private static Arbitrary<string> InvalidEmailGenerator()
    {
        var invalidEmails = Gen.OneOf(
            // Empty or whitespace
            Gen.Elements("", " ", "  ", "\t", "\n"),
            // Missing @
            Gen.Elements("invalidemail", "test.example.com", "noatsign"),
            // Missing domain part after @
            Gen.Elements("test@", "user@.com", "name@."),
            // Missing local part before @
            Gen.Elements("@example.com", "@domain.org"),
            // Contains spaces
            Gen.Elements("test @example.com", "test@ example.com", "te st@example.com"),
            // Missing TLD
            Gen.Elements("test@example", "user@domain"),
            // Multiple @ symbols
            Gen.Elements("test@@example.com", "user@domain@com"),
            // Only special characters
            Gen.Elements("@@@", "...", "@.@")
        );

        return Arb.From(invalidEmails.Where(e => !Regex.IsMatch(e, ValidEmailPattern)));
    }

    private static Arbitrary<string> InvalidPasswordGenerator()
    {
        var invalidPasswords = Gen.OneOf(
            // Empty or too short
            Gen.Elements("", "a", "Ab1", "Abc123", "Short1"),
            // Missing uppercase
            Gen.Elements("lowercase1", "nouppercase123", "alllower1"),
            // Missing lowercase
            Gen.Elements("UPPERCASE1", "NOLOWERCASE123", "ALLUPPER1"),
            // Missing number
            Gen.Elements("NoNumberHere", "AbcdefghI", "PasswordNoNum"),
            // Only numbers
            Gen.Elements("12345678", "123456789012"),
            // Only lowercase
            Gen.Elements("abcdefgh", "lowercase"),
            // Only uppercase
            Gen.Elements("ABCDEFGH", "UPPERCASE"),
            // 7 characters with all requirements (too short)
            Gen.Elements("Abcde1f", "Pass12a")
        );

        return Arb.From(invalidPasswords.Where(p => !Regex.IsMatch(p, ValidPasswordPattern)));
    }
}
