using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.DailyCheckin.Queries.GetMyTodayDailyCheckin;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.DailyCheckinQueries;

public class GetMyTodayDailyCheckinQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetMyTodayDailyCheckinQueryHandler _handler;

    public GetMyTodayDailyCheckinQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new GetMyTodayDailyCheckinQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_WhenTodayCheckinExists_ReturnsSuccess()
    {
        var userId = NewId.NextGuid();
        var today = DateTime.UtcNow.Date;
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        SetupDailyCheckinsDbSet(new List<DailyCheckins>
        {
            new() { CheckinId = NewId.NextGuid(), UserId = userId, CheckinDate = today, Mood = "Focused", Productivity = 4, CreatedAt = DateTime.UtcNow },
            new() { CheckinId = NewId.NextGuid(), UserId = userId, CheckinDate = today.AddDays(-1), Mood = "Neutral", Productivity = 3, CreatedAt = DateTime.UtcNow }
        });

        var result = await _handler.Handle(new GetMyTodayDailyCheckinQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(userId, result.Value!.UserId);
        Assert.Equal(today, result.Value.CheckinDate);
    }

    [Fact]
    public async Task Handle_WhenNoTodayCheckin_ReturnsNotFound()
    {
        var userId = NewId.NextGuid();
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        SetupDailyCheckinsDbSet(new List<DailyCheckins>());

        var result = await _handler.Handle(new GetMyTodayDailyCheckinQuery(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("DAILY_CHECKIN_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_WhenUserContextInvalid_ReturnsUnauthorized()
    {
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(Guid.Empty);
        SetupDailyCheckinsDbSet(new List<DailyCheckins>());

        var result = await _handler.Handle(new GetMyTodayDailyCheckinQuery(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
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
}
