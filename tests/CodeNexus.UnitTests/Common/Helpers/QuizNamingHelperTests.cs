using CodeNexus.Application.Common.Helpers;
using CodeNexus.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace CodeNexus.UnitTests.Common.Helpers;

public class QuizNamingHelperTests
{
    [Fact]
    public void EnsureRelatedTitle_WhenAiReturnsLessonTitle_ShouldFallbackToDifferentTitle()
    {
        // Arrange
        const string lessonTitle = "Python Data Pipeline Basics";

        // Act
        var result = QuizNamingHelper.EnsureRelatedTitle(
            lessonTitle,
            lessonTitle,
            quizIndex: 0,
            LanguageSelection.English);

        // Assert
        result.Should().NotBe(lessonTitle);
        result.Should().Contain("Python");
    }

    [Fact]
    public void BuildFinalTitles_WhenAiReturnsDuplicateAndUnrelatedTitles_ShouldReturnDistinctRelatedTitles()
    {
        // Arrange
        var aiTitles = new[] { "Practice", "Practice", "Totally other topic" };
        const string lessonTitle = "C# Dependency Injection";

        // Act
        var result = QuizNamingHelper.BuildFinalTitles(
            aiTitles,
            lessonTitle,
            quizCount: 3,
            LanguageSelection.English);

        // Assert
        result.Should().HaveCount(3);
        result.Should().OnlyHaveUniqueItems();
        result.Should().OnlyContain(title => title.Contains("Dependency", StringComparison.OrdinalIgnoreCase)
                                            || title.Contains("Injection", StringComparison.OrdinalIgnoreCase)
                                            || title.Contains("C#", StringComparison.OrdinalIgnoreCase));
    }
}
