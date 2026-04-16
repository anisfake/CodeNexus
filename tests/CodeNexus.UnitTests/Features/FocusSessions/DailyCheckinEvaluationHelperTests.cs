using CodeNexus.Application.Common.Helpers;
using Xunit;

namespace CodeNexus.UnitTests.Features.FocusSessions;

public class DailyCheckinEvaluationHelperTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData(0, null)]
    [InlineData(1, "productivity.first_step")]
    [InlineData(2, "productivity.warming_up")]
    [InlineData(3, "productivity.keep_going")]
    [InlineData(4, "productivity.building_momentum")]
    [InlineData(5, "productivity.halfway_there")]
    [InlineData(6, "productivity.good_progress")]
    [InlineData(7, "productivity.strong_effort")]
    [InlineData(8, "productivity.on_fire")]
    [InlineData(9, "productivity.almost_outstanding")]
    [InlineData(10, "productivity.excellent_today")]
    [InlineData(11, "productivity.super_productive")]
    [InlineData(12, "productivity.unstoppable")]
    [InlineData(13, "productivity.learning_machine")]
    [InlineData(14, "productivity.legendary_focus")]
    [InlineData(15, "productivity.maximum_overdrive")]
    [InlineData(99, "productivity.maximum_overdrive")]
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

