using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.FocusSessions.Commands.HeartbeatSession;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.FocusSessions;

public class HeartbeatSessionCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly HeartbeatSessionCommandHandler _handler;

    public HeartbeatSessionCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _handler = new HeartbeatSessionCommandHandler(_mockContext.Object);
    }

    [Fact]
    public async Task Handle_WhenSessionRunning_ShouldUpdateLastActivity()
    {
        var sessionId = Guid.NewGuid();
        var session = new FocusSession
        {
            SessionId = sessionId,
            SessionStatus = SessionStatus.Running,
            LastActivityAt = DateTime.UtcNow.AddMinutes(-5)
        };

        SetupFocusSessionsDbSet(new List<FocusSession> { session });
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var command = new HeartbeatSessionCommand(sessionId);
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(sessionId, result.Value!.SessionId);
        Assert.True(result.Value.LastActivityAt >= DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task Handle_WhenSessionNotFound_ShouldReturnFailure()
    {
        SetupFocusSessionsDbSet(new List<FocusSession>());

        var command = new HeartbeatSessionCommand(Guid.NewGuid());
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("SESSION_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task Handle_WhenSessionCompleted_ShouldReturnNotActive()
    {
        var sessionId = Guid.NewGuid();
        var session = new FocusSession
        {
            SessionId = sessionId,
            SessionStatus = SessionStatus.CompletedOnTime
        };

        SetupFocusSessionsDbSet(new List<FocusSession> { session });

        var command = new HeartbeatSessionCommand(sessionId);
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal("SESSION_NOT_ACTIVE", result.ErrorCode);
    }

    private void SetupFocusSessionsDbSet(List<FocusSession> sessions)
    {
        var queryable = new TestAsyncEnumerable<FocusSession>(sessions);
        var dbSetMock = new Mock<DbSet<FocusSession>>();
        dbSetMock.As<IQueryable<FocusSession>>().Setup(m => m.Provider).Returns(queryable.AsQueryable().Provider);
        dbSetMock.As<IQueryable<FocusSession>>().Setup(m => m.Expression).Returns(queryable.AsQueryable().Expression);
        dbSetMock.As<IQueryable<FocusSession>>().Setup(m => m.ElementType).Returns(queryable.AsQueryable().ElementType);
        dbSetMock.As<IQueryable<FocusSession>>().Setup(m => m.GetEnumerator()).Returns(queryable.AsQueryable().GetEnumerator());
        dbSetMock.As<IAsyncEnumerable<FocusSession>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(queryable.GetAsyncEnumerator());
        _mockContext.Setup(x => x.FocusSessions).Returns(dbSetMock.Object);
    }
}
