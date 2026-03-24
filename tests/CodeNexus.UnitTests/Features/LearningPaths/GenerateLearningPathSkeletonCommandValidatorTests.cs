using CodeNexus.Application.Features.LearningPathSkeleton.Commands.GenerateLearningPathSkeleton;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Enums;
using FluentValidation.TestHelper;
using System.Collections.Generic;
using Xunit;

namespace CodeNexus.UnitTests.Features.LearningPaths;

public class GenerateLearningPathSkeletonCommandValidatorTests
{
    private readonly GenerateLearningPathSkeletonCommandValidator _validator;

    public GenerateLearningPathSkeletonCommandValidatorTests()
    {
        _validator = new GenerateLearningPathSkeletonCommandValidator();
    }

    [Fact]
    public void Validate_WithValidCommand_ShouldNotHaveErrors()
    {
        // Arrange
        var goals = new List<LearningPathGoalRequest> { new(Guid.NewGuid(), 1m) };
        var command = new GenerateLearningPathSkeletonCommand(Guid.NewGuid(), goals, ComplexityLevel.Beginner, LanguageSelection.VietNamese);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptySubjectId_ShouldHaveError()
    {
        // Arrange
        var goals = new List<LearningPathGoalRequest> { new(Guid.NewGuid(), 1m) };
        var command = new GenerateLearningPathSkeletonCommand(Guid.Empty, goals, ComplexityLevel.Intermediate, LanguageSelection.English);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SubjectId);
    }

    [Fact]
    public void Validate_WithEmptyGoals_ShouldHaveError()
    {
        // Arrange
        var command = new GenerateLearningPathSkeletonCommand(Guid.NewGuid(), new List<LearningPathGoalRequest>(), ComplexityLevel.Advanced, LanguageSelection.VietNamese);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Goals);
    }

    [Fact]
    public void Validate_WithEmptySubjectAndGoals_ShouldHaveErrors()
    {
        // Arrange
        var command = new GenerateLearningPathSkeletonCommand(Guid.Empty, new List<LearningPathGoalRequest>(), ComplexityLevel.Beginner, LanguageSelection.VietNamese);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SubjectId);
        result.ShouldHaveValidationErrorFor(x => x.Goals);
    }

    [Fact]
    public void Validate_WithInvalidComplexityLevel_ShouldHaveError()
    {
        // Arrange
        var goals = new List<LearningPathGoalRequest> { new(Guid.NewGuid(), 1m) };
        var command = new GenerateLearningPathSkeletonCommand(Guid.NewGuid(), goals, (ComplexityLevel)999, LanguageSelection.VietNamese);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ComplexityLevel);
    }
}
