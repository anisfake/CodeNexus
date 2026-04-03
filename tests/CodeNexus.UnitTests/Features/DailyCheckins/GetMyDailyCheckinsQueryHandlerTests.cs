using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.DailyCheckin.Queries.GetMyDailyCheckins;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using TaskEntity = CodeNexus.Domain.Entities.Tasks;

namespace CodeNexus.UnitTests.Features.DailyCheckinQueries;

public class GetMyDailyCheckinsQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetMyDailyCheckinsQueryHandler _handler;

    public GetMyDailyCheckinsQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new GetMyDailyCheckinsQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_WhenUserHasCheckins_ReturnsFilteredCheckins()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var query = new GetMyDailyCheckinsQuery(DateTime.UtcNow.Date.AddDays(-7), DateTime.UtcNow.Date, 1, 20);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);

        var inRange = BuildCheckin(userId, NewId.NextGuid(), DateTime.UtcNow.Date.AddDays(-2));
        var outOfRange = BuildCheckin(userId, NewId.NextGuid(), DateTime.UtcNow.Date.AddDays(-10));
        var otherUser = BuildCheckin(NewId.NextGuid(), NewId.NextGuid(), DateTime.UtcNow.Date.AddDays(-1));

        SetupDailyCheckinsDbSet(new List<DailyCheckins> { inRange, outOfRange, otherUser });

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(inRange.SessionId, result.Value[0].SessionId);
    }

    [Fact]
    public async Task Handle_WhenUserContextInvalid_ReturnsUnauthorized()
    {
        // Arrange
        var query = new GetMyDailyCheckinsQuery();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(Guid.Empty);
        SetupDailyCheckinsDbSet(new List<DailyCheckins>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
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
