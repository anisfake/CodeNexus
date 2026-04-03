using CodeNexus.Application.Common.Helpers;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using Xunit;

namespace CodeNexus.UnitTests.Features.FocusSessions;

public class DailyCheckinEvaluationHelperTests
{
    [Fact]
    public void Evaluate_WithStrongSessionMetrics_ShouldReturnHighProductivityAndMotivatedMood()
    {
        // Arrange
        var session = new FocusSession
        {
            SessionStatus = SessionStatus.CompletedOnTime,
            PlannedDurationMinutes = 60,
            ActualDurationMinutes = 58,
            TotalPausedMinutes = 2,
            VerificationScore = 90
        };

        // Act
        var (mood, productivity) = DailyCheckinEvaluationHelper.Evaluate(session);

        // Assert
        Assert.Equal("Motivated", mood);
        Assert.Equal(5, productivity);
    }

    [Fact]
    public void Evaluate_WithWeakSessionMetrics_ShouldReturnLowestProductivityAndFrustratedMood()
    {
        // Arrange
        var session = new FocusSession
        {
            SessionStatus = SessionStatus.CompletedEarly,
            PlannedDurationMinutes = 60,
            ActualDurationMinutes = 20,
            TotalPausedMinutes = 10,
            VerificationScore = 45
        };

        // Act
        var (mood, productivity) = DailyCheckinEvaluationHelper.Evaluate(session);

        // Assert
        Assert.Equal("Frustrated", mood);
        Assert.Equal(1, productivity);
    }

    [Fact]
    public void Evaluate_WithPlannedDurationZero_ShouldNotDivideByZeroAndReturnValidRange()
    {
        // Arrange
        var session = new FocusSession
        {
            SessionStatus = SessionStatus.CompletedOnTime,
            PlannedDurationMinutes = 0,
            ActualDurationMinutes = 30,
            TotalPausedMinutes = 3,
            VerificationScore = null
        };

        // Act
        var (mood, productivity) = DailyCheckinEvaluationHelper.Evaluate(session);

        // Assert
        Assert.Contains(mood, new[] { "Motivated", "Focused", "Neutral", "Tired", "Frustrated" });
        Assert.InRange(productivity, 1, 5);
    }
}
