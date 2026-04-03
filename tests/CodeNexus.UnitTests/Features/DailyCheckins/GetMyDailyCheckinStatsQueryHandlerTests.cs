using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.DailyCheckin.Queries.GetMyDailyCheckinStats;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using TaskEntity = CodeNexus.Domain.Entities.Tasks;

namespace CodeNexus.UnitTests.Features.DailyCheckinQueries;

public class GetMyDailyCheckinStatsQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetMyDailyCheckinStatsQueryHandler _handler;

    public GetMyDailyCheckinStatsQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new GetMyDailyCheckinStatsQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_WhenUserContextInvalid_ReturnsUnauthorized()
    {
        // Arrange
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(Guid.Empty);
        SetupDailyCheckinsDbSet(new List<DailyCheckins>());

        // Act
        var result = await _handler.Handle(new GetMyDailyCheckinStatsQuery(), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_WhenNoCheckins_ReturnsZeroStats()
    {
        // Arrange
        var userId = NewId.NextGuid();
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        SetupDailyCheckinsDbSet(new List<DailyCheckins>());

        // Act
        var result = await _handler.Handle(new GetMyDailyCheckinStatsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.False(result.Value.TodayCheckedIn);
        Assert.Equal(0, result.Value.CurrentStreak);
        Assert.Equal(0, result.Value.LongestStreak);
        Assert.Equal(0, result.Value.TotalCheckins);
        Assert.Null(result.Value.LastCheckinDate);
        Assert.False(result.Value.IsStreakMilestone);
        Assert.Equal("DAILY_CHECKIN_NOT_DONE_TODAY", result.Value.PopupCode);
        Assert.Null(result.Value.PopupParams);
    }

    [Fact]
    public async Task Handle_WhenHasCheckins_ReturnsCorrectStreakStats()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var today = DateTime.UtcNow.Date;
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        var checkins = new List<DailyCheckins>
        {
            BuildCheckin(userId, NewId.NextGuid(), today),
            BuildCheckin(userId, NewId.NextGuid(), today.AddDays(-1)),
            BuildCheckin(userId, NewId.NextGuid(), today.AddDays(-3)),
            BuildCheckin(userId, NewId.NextGuid(), today.AddDays(-4)),
            BuildCheckin(userId, NewId.NextGuid(), today.AddDays(-5)),
            BuildCheckin(NewId.NextGuid(), NewId.NextGuid(), today)
        };

        SetupDailyCheckinsDbSet(checkins);

        // Act
        var result = await _handler.Handle(new GetMyDailyCheckinStatsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.True(result.Value.TodayCheckedIn);
        Assert.Equal(2, result.Value.CurrentStreak);
        Assert.Equal(3, result.Value.LongestStreak);
        Assert.Equal(5, result.Value.TotalCheckins);
        Assert.Equal(today, result.Value.LastCheckinDate);
        Assert.False(result.Value.IsStreakMilestone);
        Assert.Equal("DAILY_CHECKIN_DONE_TODAY_STREAK", result.Value.PopupCode);
        Assert.NotNull(result.Value.PopupParams);
        Assert.Equal("2", result.Value.PopupParams!["currentStreak"]);
    }

    [Fact]
    public async Task Handle_WhenReachMilestoneStreak_ReturnsMilestonePopup()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var today = DateTime.UtcNow.Date;
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        var checkins = new List<DailyCheckins>
        {
            BuildCheckin(userId, NewId.NextGuid(), today),
            BuildCheckin(userId, NewId.NextGuid(), today.AddDays(-1)),
            BuildCheckin(userId, NewId.NextGuid(), today.AddDays(-2))
        };

        SetupDailyCheckinsDbSet(checkins);

        // Act
        var result = await _handler.Handle(new GetMyDailyCheckinStatsQuery(), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsStreakMilestone);
        Assert.Equal("DAILY_CHECKIN_STREAK_MILESTONE", result.Value.PopupCode);
        Assert.NotNull(result.Value.PopupParams);
        Assert.Equal("3", result.Value.PopupParams!["currentStreak"]);
    }

    private void SetupDailyCheckinsDbSet(List<DailyCheckins> checkins)
    {
        var queryable = new TestAsyncEnumerable<DailyCheckins>(checkins);
        var dbSetMock = new Mock<DbSet<DailyCheckins>>();
        dbSetMock.As<IQueryable<DailyCheckins>>().Setup(m => m.Provider).Returns(queryable.AsQueryable().Provider);
        dbSetMock.As<IQueryable<DailyCheckins>>().Setup(m => m.Expression).Returns(queryable.AsQueryable().Expression);
        dbSetMock.As<IQueryable<DailyCheckins>>().Setup(m => m.ElementType).Returns(queryable.AsQueryable().ElementType);
        dbSetMock.As<IQueryable<DailyCheckins>>().Setup(m => m.GetEnumerator()).Returns(queryable.AsQueryable().GetEnumerator());
        dbSetMock.As<IAsyncEnumerable<DailyCheckins>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(queryable.GetAsyncEnumerator());
        _mockContext.Setup(x => x.DailyCheckins).Returns(dbSetMock.Object);
    }

    private static DailyCheckins BuildCheckin(Guid userId, Guid sessionId, DateTime checkinDate)
    {
        var pathId = NewId.NextGuid();
        var taskId = NewId.NextGuid();
        var subjectId = NewId.NextGuid();

        return new DailyCheckins
        {
            CheckinId = NewId.NextGuid(),
            SessionId = sessionId,
            CheckinDate = checkinDate,
            Mood = "Focused",
            Productivity = 4,
            CreatedAt = DateTime.UtcNow,
            FocusSession = new FocusSession
            {
                SessionId = sessionId,
                TaskId = taskId,
                Task = new TaskEntity
                {
                    TaskId = taskId,
                    PathId = pathId,
                    LearningPath = new LearningPath
                    {
                        PathId = pathId,
                        UserId = userId,
                        SubjectId = subjectId,
                        Subject = new Subject
                        {
                            SubjectId = subjectId,
                            Name = "Test Subject"
                        },
                        Title = "Test Path"
                    }
                }
            }
        };
    }
}
