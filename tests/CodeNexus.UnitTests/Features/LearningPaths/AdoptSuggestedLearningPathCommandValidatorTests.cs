using CodeNexus.Application.Features.LearningPathSkeleton.Commands.AdoptSuggestedLearningPath;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Enums;
using FluentValidation.TestHelper;
using Xunit;

namespace CodeNexus.UnitTests.Features.LearningPaths;

public class AdoptSuggestedLearningPathCommandValidatorTests
{
    private readonly AdoptSuggestedLearningPathCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_ShouldNotHaveErrors()
    {
        var command = new AdoptSuggestedLearningPathCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new List<LearningPathGoalRequest> { new(Guid.NewGuid(), 1m) },
            ComplexityLevel.Beginner,
            LanguageSelection.VietNamese);

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptySuggestedPathId_ShouldHaveError()
    {
        var command = new AdoptSuggestedLearningPathCommand(
            Guid.Empty,
            Guid.NewGuid(),
            new List<LearningPathGoalRequest> { new(Guid.NewGuid(), 1m) },
            ComplexityLevel.Beginner,
            LanguageSelection.English);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.SuggestedPathId);
    }

    [Fact]
    public void Validate_WithMoreThanTwoGoals_ShouldHaveError()
    {
        var command = new AdoptSuggestedLearningPathCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new List<LearningPathGoalRequest>
            {
                new(Guid.NewGuid(), 0.5m),
                new(Guid.NewGuid(), 0.3m),
                new(Guid.NewGuid(), 0.2m)
            },
            ComplexityLevel.Intermediate,
            LanguageSelection.English);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Goals);
    }

    [Fact]
    public void Validate_WithZeroTotalWeight_ShouldHaveError()
    {
        var command = new AdoptSuggestedLearningPathCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new List<LearningPathGoalRequest>
            {
                new(Guid.NewGuid(), 0m)
            },
            ComplexityLevel.Intermediate,
            LanguageSelection.English);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Goals);
    }
}
