using CodeNexus.Application.Common.Helpers;
using Xunit;

namespace CodeNexus.UnitTests.Features.FocusSessions;

public class DailyCheckinEvaluationHelperTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData(0, null)]
    [InlineData(1, "productivity.keep_going")]
    [InlineData(2, "productivity.keep_going")]
    [InlineData(3, "productivity.good_progress")]
    [InlineData(5, "productivity.good_progress")]
    [InlineData(6, "productivity.excellent_today")]
    [InlineData(10, "productivity.excellent_today")]
    public void MapProductivityKey_ShouldReturnCorrectKey(int? activityCount, string? expectedKey)
    {
        var key = DailyCheckinEvaluationHelper.MapProductivityKey(activityCount);
        Assert.Equal(expectedKey, key);
    }

    [Fact]
    public void IncrementActivityCount_WithNull_ShouldStartFromZero()
    {
        var result = DailyCheckinEvaluationHelper.IncrementActivityCount(null, 1);
        Assert.Equal(1, result);
    }

    [Fact]
    public void IncrementActivityCount_WithExistingValue_ShouldAccumulate()
    {
        var result = DailyCheckinEvaluationHelper.IncrementActivityCount(3, 2);
        Assert.Equal(5, result);
    }
}

