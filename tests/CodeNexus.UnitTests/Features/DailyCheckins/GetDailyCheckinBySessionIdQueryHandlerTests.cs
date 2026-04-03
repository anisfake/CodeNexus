using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.DailyCheckin.Queries.GetDailyCheckinBySessionId;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using TaskEntity = CodeNexus.Domain.Entities.Tasks;

namespace CodeNexus.UnitTests.Features.DailyCheckinQueries;

public class GetDailyCheckinBySessionIdQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetDailyCheckinBySessionIdQueryHandler _handler;

    public GetDailyCheckinBySessionIdQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new GetDailyCheckinBySessionIdQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_WhenCheckinExistsForCurrentUser_ReturnsSuccess()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var sessionId = NewId.NextGuid();
        var query = new GetDailyCheckinBySessionIdQuery(sessionId);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        SetupDailyCheckinsDbSet(new List<DailyCheckins> { BuildCheckin(userId, sessionId) });

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(sessionId, result.Value.SessionId);
    }

    [Fact]
    public async Task Handle_WhenCheckinNotExists_ReturnsNotFound()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var query = new GetDailyCheckinBySessionIdQuery(NewId.NextGuid());

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        SetupDailyCheckinsDbSet(new List<DailyCheckins>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("DAILY_CHECKIN_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_WhenUserContextInvalid_ReturnsUnauthorized()
    {
        // Arrange
        var query = new GetDailyCheckinBySessionIdQuery(NewId.NextGuid());

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

    private static DailyCheckins BuildCheckin(Guid userId, Guid sessionId)
    {
        var pathId = NewId.NextGuid();
        var taskId = NewId.NextGuid();

        return new DailyCheckins
        {
            CheckinId = NewId.NextGuid(),
            SessionId = sessionId,
            CheckinDate = DateTime.UtcNow.Date,
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
                        SubjectId = NewId.NextGuid(),
                        Subject = new Subject
                        {
                            SubjectId = NewId.NextGuid(),
                            Name = "Test Subject"
                        },
                        Title = "Test Path"
                    }
                }
            }
        };
    }
}
