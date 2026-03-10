using CodeNexus.Application.Features.FocusSessions.Commands.CompleteSession;
using Xunit;

namespace CodeNexus.UnitTests.Features.FocusSessions;

public class CompleteSessionCommandValidatorTests
{
    private readonly CompleteSessionCommandValidator _validator;

    public CompleteSessionCommandValidatorTests()
    {
        _validator = new CompleteSessionCommandValidator();
    }

    [Fact]
    public void Validate_WithValidCommand_ShouldPass()
    {
        // Arrange
        var command = new CompleteSessionCommand(
            Guid.NewGuid(),
            "console.log('Hello World');",
            null,
            false);

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WithEmptySessionId_ShouldFail()
    {
        // Arrange
        var command = new CompleteSessionCommand(
            Guid.Empty,
            "console.log('Hello World');",
            null,
            false);

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "SessionId" && e.ErrorMessage == "SessionId is required");
    }

    [Fact]
    public void Validate_WithTooLongSubmittedCode_ShouldFail()
    {
        // Arrange
        var longCode = new string('a', 10001); // Exceeds 10,000 character limit
        var command = new CompleteSessionCommand(
            Guid.NewGuid(),
            longCode,
            null,
            false);

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "SubmittedCode" && 
            e.ErrorMessage == "Submitted code cannot exceed 10,000 characters");
    }

    [Fact]
    public void Validate_WithTooLongSubmittedSummary_ShouldFail()
    {
        // Arrange
        var longSummary = new string('a', 2001); // Exceeds 2,000 character limit
        var command = new CompleteSessionCommand(
            Guid.NewGuid(),
            null,
            longSummary,
            false);

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "SubmittedSummary" && 
            e.ErrorMessage == "Submitted summary cannot exceed 2,000 characters");
    }

    [Fact]
    public void Validate_WithValidCodeLength_ShouldPass()
    {
        // Arrange
        var validCode = new string('a', 5000); // Within 10,000 character limit
        var command = new CompleteSessionCommand(
            Guid.NewGuid(),
            validCode,
            null,
            false);

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WithValidSummaryLength_ShouldPass()
    {
        // Arrange
        var validSummary = new string('a', 1000); // Within 2,000 character limit
        var command = new CompleteSessionCommand(
            Guid.NewGuid(),
            null,
            validSummary,
            false);

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WithNullCodeAndSummary_ShouldPass()
    {
        // Arrange
        var command = new CompleteSessionCommand(
            Guid.NewGuid(),
            null,
            null,
            true);

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WithEmptyStrings_ShouldPass()
    {
        // Arrange
        var command = new CompleteSessionCommand(
            Guid.NewGuid(),
            "",
            "",
            false);

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WithCodeContainingSpecialCharacters_ShouldPass()
    {
        // Arrange
        var codeWithSpecialChars = @"using System;
namespace Test
{
    class Program
    {
        static void Main()
        {
            Console.WriteLine(""Hello\nWorld!"");
            var text = ""This is a test with 'quotes' and \""double quotes\""."";
        }
    }
}";
        var command = new CompleteSessionCommand(
            Guid.NewGuid(),
            codeWithSpecialChars,
            null,
            false);

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WithSummaryContainingSpecialCharacters_ShouldPass()
    {
        // Arrange
        var summaryWithSpecialChars = @"I learned about:
1. Variables and data types
2. Control structures (if/else, loops)
3. Functions and methods
4. Object-oriented programming concepts

Key takeaways:
- Always validate input
- Use proper naming conventions
- Write clean, readable code";

        var command = new CompleteSessionCommand(
            Guid.NewGuid(),
            null,
            summaryWithSpecialChars,
            false);

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WithBothCodeAndSummary_ShouldPass()
    {
        // Arrange
        var command = new CompleteSessionCommand(
            Guid.NewGuid(),
            "console.log('Hello');",
            "I learned about console output",
            true);

        // Act
        var result = _validator.Validate(command);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
}