using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.DailyCheckin.Queries.GetMyDailyCheckinStatus;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.DailyCheckinQueries;

public class GetMyDailyCheckinStatusQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetMyDailyCheckinStatusQueryHandler _handler;

    public GetMyDailyCheckinStatusQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new GetMyDailyCheckinStatusQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_WhenUserContextInvalid_ReturnsUnauthorized()
    {
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(Guid.Empty);
        SetupDailyCheckinsDbSet(new List<DailyCheckins>());

        var result = await _handler.Handle(new GetMyDailyCheckinStatusQuery(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("UNAUTHORIZED", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_WhenNoCheckins_ReturnsNotCheckedInAndZeroStreak()
    {
        var userId = NewId.NextGuid();
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        SetupDailyCheckinsDbSet(new List<DailyCheckins>());

        var result = await _handler.Handle(new GetMyDailyCheckinStatusQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.TodayCheckedIn);
        Assert.Equal(0, result.Value.CurrentStreak);
    }

    [Fact]
    public async Task Handle_WhenHasConsecutiveCheckins_ReturnsCheckedInAndStreak()
    {
        var userId = NewId.NextGuid();
        var today = DateTime.UtcNow.Date;
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        SetupDailyCheckinsDbSet(new List<DailyCheckins>
        {
            new() { CheckinId = NewId.NextGuid(), UserId = userId, CheckinDate = today, CreatedAt = DateTime.UtcNow },
            new() { CheckinId = NewId.NextGuid(), UserId = userId, CheckinDate = today.AddDays(-1), CreatedAt = DateTime.UtcNow },
            new() { CheckinId = NewId.NextGuid(), UserId = NewId.NextGuid(), CheckinDate = today, CreatedAt = DateTime.UtcNow }
        });

        var result = await _handler.Handle(new GetMyDailyCheckinStatusQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.TodayCheckedIn);
        Assert.Equal(2, result.Value.CurrentStreak);
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
