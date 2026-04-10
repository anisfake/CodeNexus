using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Users.Services;
using CodeNexus.Domain.Entities;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;

namespace CodeNexus.UnitTests.Features.Users;

public class DailyReminderTimeInferenceServiceTests
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<IDateTimeProvider> _dateTimeProviderMock;
    private readonly DailyReminderTimeInferenceService _service;

    public DailyReminderTimeInferenceServiceTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _dateTimeProviderMock = new Mock<IDateTimeProvider>();
        _service = new DailyReminderTimeInferenceService(_contextMock.Object, _dateTimeProviderMock.Object);
    }

    [Fact]
    public async Task InferDailyReminderTimeAsync_WhenRecentSessionsExist_ReturnsMostFrequentHour()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var now = new DateTime(2026, 4, 10, 12, 0, 0, DateTimeKind.Utc);
        _dateTimeProviderMock.Setup(x => x.UtcNow).Returns(now);

        var sessions = new List<FocusSession>
        {
            CreateSession(userId, now.AddDays(-1).Date.AddHours(13)),
            CreateSession(userId, now.AddDays(-2).Date.AddHours(13).AddMinutes(10)),
            CreateSession(userId, now.AddDays(-3).Date.AddHours(2)),
            CreateSession(userId, now.AddDays(-4).Date.AddHours(13).AddMinutes(45)),
            CreateSession(userId, now.AddDays(-8).Date.AddHours(7))
        };

        _contextMock.Setup(x => x.FocusSessions).Returns(sessions.AsQueryable().BuildMockDbSet().Object);

        // Act
        var result = await _service.InferDailyReminderTimeAsync(userId, CancellationToken.None);

        // Assert
        result.Should().Be(new TimeSpan(20, 0, 0));
    }

    [Fact]
    public async Task InferDailyReminderTimeAsync_WhenNoRecentSessions_ReturnsDefault20h()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var now = new DateTime(2026, 4, 10, 12, 0, 0, DateTimeKind.Utc);
        _dateTimeProviderMock.Setup(x => x.UtcNow).Returns(now);

        var sessions = new List<FocusSession>
        {
            CreateSession(userId, now.AddDays(-10).Date.AddHours(21))
        };

        _contextMock.Setup(x => x.FocusSessions).Returns(sessions.AsQueryable().BuildMockDbSet().Object);

        // Act
        var result = await _service.InferDailyReminderTimeAsync(userId, CancellationToken.None);

        // Assert
        result.Should().Be(new TimeSpan(20, 0, 0));
    }

    private static FocusSession CreateSession(Guid userId, DateTime startUtc)
    {
        var pathId = NewId.NextGuid();
        var chapterId = NewId.NextGuid();

        return new FocusSession
        {
            SessionId = NewId.NextGuid(),
            StartTime = startUtc,
            Task = new Domain.Entities.Tasks
            {
                TaskId = NewId.NextGuid(),
                ChapterId = chapterId,
                PathId = pathId,
                LearningPath = new LearningPath
                {
                    PathId = pathId,
                    UserId = userId,
                    Title = "Path"
                },
                Chapter = new Chapter
                {
                    ChapterId = chapterId,
                    PathId = pathId,
                    Title = "Chapter"
                },
                Title = "Task"
            }
        };
    }
}
