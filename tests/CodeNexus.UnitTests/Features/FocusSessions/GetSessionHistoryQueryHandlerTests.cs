using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.FocusSessions.Queries.GetSessionHistory;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using Moq;
using Xunit;
using TaskEntity = CodeNexus.Domain.Entities.Tasks;

namespace CodeNexus.UnitTests.Features.FocusSessions;

public class GetSessionHistoryQueryHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly GetSessionHistoryQueryHandler _handler;

    public GetSessionHistoryQueryHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _handler = new GetSessionHistoryQueryHandler(_mockContext.Object, _mockCurrentUserService.Object);
    }

    [Fact]
    public async Task Handle_DefaultFilter_ShouldReturnOnlyEndedNonAbandonedSessions()
    {
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var ownerPath = new LearningPath
        {
            PathId = Guid.NewGuid(),
            UserId = ownerId,
            Title = "Owner Path"
        };

        var otherPath = new LearningPath
        {
            PathId = Guid.NewGuid(),
            UserId = otherUserId,
            Title = "Other Path"
        };

        var ownerChapter = new Chapter { ChapterId = Guid.NewGuid(), PathId = ownerPath.PathId, Title = "Owner Chapter", LearningPath = ownerPath };
        var otherChapter = new Chapter { ChapterId = Guid.NewGuid(), PathId = otherPath.PathId, Title = "Other Chapter", LearningPath = otherPath };

        var ownerTask = new TaskEntity
        {
            TaskId = Guid.NewGuid(),
            PathId = ownerPath.PathId,
            LearningPath = ownerPath,
            ChapterId = ownerChapter.ChapterId,
            Chapter = ownerChapter,
            Title = "Owner Task"
        };

        var otherTask = new TaskEntity
        {
            TaskId = Guid.NewGuid(),
            PathId = otherPath.PathId,
            LearningPath = otherPath,
            ChapterId = otherChapter.ChapterId,
            Chapter = otherChapter,
            Title = "Other Task"
        };

        var now = DateTime.UtcNow;
        var sessions = new List<FocusSession>
        {
            new()
            {
                SessionId = Guid.NewGuid(),
                TaskId = ownerTask.TaskId,
                Task = ownerTask,
                Title = "Completed",
                SessionStatus = SessionStatus.CompletedOnTime,
                SessionType = SessionType.Study,
                StartTime = now.AddHours(-3),
                EndTime = now.AddHours(-2),
                PlannedDurationMinutes = 60,
                ActualDurationMinutes = 55
            },
            new()
            {
                SessionId = Guid.NewGuid(),
                TaskId = ownerTask.TaskId,
                Task = ownerTask,
                Title = "Abandoned",
                SessionStatus = SessionStatus.Abandoned,
                SessionType = SessionType.Study,
                StartTime = now.AddHours(-4),
                EndTime = now.AddHours(-3),
                PlannedDurationMinutes = 60,
                ActualDurationMinutes = 10
            },
            new()
            {
                SessionId = Guid.NewGuid(),
                TaskId = ownerTask.TaskId,
                Task = ownerTask,
                Title = "Running",
                SessionStatus = SessionStatus.Running,
                SessionType = SessionType.Pomodoro,
                StartTime = now.AddMinutes(-30),
                PlannedDurationMinutes = 25
            },
            new()
            {
                SessionId = Guid.NewGuid(),
                TaskId = otherTask.TaskId,
                Task = otherTask,
                Title = "Other User Session",
                SessionStatus = SessionStatus.CompletedOnTime,
                SessionType = SessionType.Study,
                StartTime = now.AddHours(-5),
                EndTime = now.AddHours(-4),
                PlannedDurationMinutes = 45,
                ActualDurationMinutes = 40
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(ownerId);
        _mockContext.Setup(x => x.FocusSessions).Returns(sessions.BuildMockDbSet().Object);

        var query = new GetSessionHistoryQuery(PageNumber: 1, PageSize: 20);
        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(result.Value!.Items);
        Assert.Equal("Completed", result.Value.Items[0].Title);
        Assert.Equal(1, result.Value.TotalCount);
    }

    [Fact]
    public async Task Handle_StatusFilterAbandoned_ShouldReturnAbandonedSessions()
    {
        var ownerId = Guid.NewGuid();
        var path = new LearningPath { PathId = Guid.NewGuid(), UserId = ownerId, Title = "Path" };
        var chapter = new Chapter { ChapterId = Guid.NewGuid(), PathId = path.PathId, Title = "Chapter", LearningPath = path };
        var task = new TaskEntity
        {
            TaskId = Guid.NewGuid(),
            PathId = path.PathId,
            LearningPath = path,
            ChapterId = chapter.ChapterId,
            Chapter = chapter,
            Title = "Task"
        };

        var sessions = new List<FocusSession>
        {
            new()
            {
                SessionId = Guid.NewGuid(),
                TaskId = task.TaskId,
                Task = task,
                Title = "A1",
                SessionStatus = SessionStatus.Abandoned,
                SessionType = SessionType.Study,
                StartTime = DateTime.UtcNow.AddHours(-1),
                EndTime = DateTime.UtcNow
            },
            new()
            {
                SessionId = Guid.NewGuid(),
                TaskId = task.TaskId,
                Task = task,
                Title = "C1",
                SessionStatus = SessionStatus.CompletedEarly,
                SessionType = SessionType.Study,
                StartTime = DateTime.UtcNow.AddHours(-2),
                EndTime = DateTime.UtcNow.AddHours(-1)
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(ownerId);
        _mockContext.Setup(x => x.FocusSessions).Returns(sessions.BuildMockDbSet().Object);

        var query = new GetSessionHistoryQuery(
            SessionStatus: SessionStatus.Abandoned,
            IncludeAbandoned: false,
            PageNumber: 1,
            PageSize: 20);

        var result = await _handler.Handle(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Single(result.Value!.Items);
        Assert.Equal("A1", result.Value.Items[0].Title);
        Assert.Equal(SessionStatus.Abandoned.ToString(), result.Value.Items[0].SessionStatus);
    }
}
