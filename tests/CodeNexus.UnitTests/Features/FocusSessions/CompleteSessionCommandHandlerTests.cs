using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.FocusSessions.Commands.CompleteSession;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using TaskEntity = CodeNexus.Domain.Entities.Tasks;

namespace CodeNexus.UnitTests.Features.FocusSessions;

public class CompleteSessionCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ITaskVerificationService> _mockVerificationService;
    private readonly Mock<IAchievementService> _mockAchievementService;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<IPlanUsageLimitService> _mockPlanUsageLimitService;
    private readonly CompleteSessionCommandHandler _handler;

    public CompleteSessionCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockVerificationService = new Mock<ITaskVerificationService>();
        _mockAchievementService = new Mock<IAchievementService>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockPlanUsageLimitService = new Mock<IPlanUsageLimitService>();
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(Guid.NewGuid());
        _mockPlanUsageLimitService.Setup(x => x.CheckFocusSessionReviewAllowedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CodeNexus.Application.Common.Models.Result.Success());
        _mockPlanUsageLimitService.Setup(x => x.RecordFocusSessionReviewUsageAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _handler = new CompleteSessionCommandHandler(
            _mockContext.Object,
            _mockVerificationService.Object,
            _mockAchievementService.Object,
            _mockCurrentUserService.Object,
            _mockPlanUsageLimitService.Object);
    }

    [Fact]
    public async Task Handle_WithValidPracticeTask_ShouldCompleteSessionSuccessfully()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var command = new CompleteSessionCommand(sessionId, "console.log('Hello World');", null, null, false, CodeNexus.Domain.Enums.SubmissionType.Final);

        var task = new TaskEntity
        {
            TaskId = taskId,
            Title = "Practice Task",
            Description = "Write a hello world program",
            TaskType = TaskType.Practice,
            Status = TaskStatus_.InProgress,
            VerificationPrompt = "Check if code prints hello world",
            LearningPath = BuildLearningPath()
        };
        task.PathId = task.LearningPath.PathId;

        var session = new FocusSession
        {
            SessionId = sessionId,
            TaskId = taskId,
            Task = task,
            SessionStatus = SessionStatus.Running,
            StartTime = DateTime.UtcNow.AddMinutes(-20),
            PlannedDurationMinutes = 25
        };

        var verificationResult = new VerificationResult
        {
            IsPass = true,
            Score = 85,
            Feedback = "Great job! Your code works correctly."
        };

        SetupFocusSessionsDbSet(new List<FocusSession> { session });
        _mockVerificationService.Setup(x => x.VerifyCodeSubmissionAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(verificationResult);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(SessionStatus.CompletedOnTime.ToString(), result.Value.SessionStatus);
        Assert.True(result.Value.TaskCompleted);
        Assert.Equal("Great job! Your code works correctly.", result.Value.AIFeedback);
        Assert.Equal(85, result.Value.VerificationScore);
    }

    [Fact]
    public async Task Handle_WithValidTheoryTask_ShouldCompleteSessionSuccessfully()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var command = new CompleteSessionCommand(sessionId, null, "I learned about variables and data types", null, false, CodeNexus.Domain.Enums.SubmissionType.Final);

        var task = new TaskEntity
        {
            TaskId = taskId,
            Title = "Theory Task",
            Description = "Learn about variables",
            TaskType = TaskType.Theory,
            Status = TaskStatus_.InProgress,
            VerificationPrompt = "Check if summary covers key concepts",
            LearningPath = BuildLearningPath()
        };
        task.PathId = task.LearningPath.PathId;

        var session = new FocusSession
        {
            SessionId = sessionId,
            TaskId = taskId,
            Task = task,
            SessionStatus = SessionStatus.Running,
            StartTime = DateTime.UtcNow.AddMinutes(-15),
            PlannedDurationMinutes = 25
        };

        var verificationResult = new VerificationResult
        {
            IsPass = true,
            Score = 90,
            Feedback = "Excellent summary of key concepts."
        };

        SetupFocusSessionsDbSet(new List<FocusSession> { session });
        _mockVerificationService.Setup(x => x.VerifySummarySubmissionAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(verificationResult);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(SessionStatus.CompletedEarly.ToString(), result.Value.SessionStatus);
        Assert.True(result.Value.TaskCompleted);
        Assert.Equal("Excellent summary of key concepts.", result.Value.AIFeedback);
        Assert.Equal(90, result.Value.VerificationScore);
    }

    [Fact]
    public async Task Handle_WithQuizTask_ShouldCompleteWithoutVerification()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var command = new CompleteSessionCommand(sessionId, null, null, "{\"answers\": [0, 1, 2, 1]}", false, CodeNexus.Domain.Enums.SubmissionType.Final);

        var task = new TaskEntity
        {
            TaskId = taskId,
            Title = "Quiz Task",
            Description = "Complete the quiz",
            TaskType = TaskType.Quizz,
            Status = TaskStatus_.InProgress,
            QuizQuestionsJson = "[{\"question\":\"Test?\",\"options\":[\"A\",\"B\",\"C\",\"D\"],\"correctAnswer\":0}]",
            LearningPath = BuildLearningPath()
        };
        task.PathId = task.LearningPath.PathId;

        var session = new FocusSession
        {
            SessionId = sessionId,
            TaskId = taskId,
            Task = task,
            SessionStatus = SessionStatus.Running,
            StartTime = DateTime.UtcNow.AddMinutes(-25),
            PlannedDurationMinutes = 25
        };

        SetupFocusSessionsDbSet(new List<FocusSession> { session });
        _mockVerificationService.Setup(v => v.VerifyQuizSubmissionAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new VerificationResult { Score = 85, Feedback = "Good job!", IsPass = true });
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(SessionStatus.CompletedOnTime.ToString(), result.Value.SessionStatus);
        Assert.True(result.Value.TaskCompleted); // Quiz task should complete with verification
        Assert.Equal("Good job!", result.Value.AIFeedback);
        Assert.Equal(85, result.Value.VerificationScore);
    }

    [Fact]
    public async Task Handle_WithEarlyCompletion_ShouldSetCorrectStatus()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var command = new CompleteSessionCommand(sessionId, "console.log('Hello');", null, null, true, CodeNexus.Domain.Enums.SubmissionType.Final);

        var task = new TaskEntity
        {
            TaskId = taskId,
            Title = "Practice Task",
            TaskType = TaskType.Practice,
            Status = TaskStatus_.InProgress,
            VerificationPrompt = "Check code",
            LearningPath = BuildLearningPath()
        };
        task.PathId = task.LearningPath.PathId;

        var session = new FocusSession
        {
            SessionId = sessionId,
            TaskId = taskId,
            Task = task,
            SessionStatus = SessionStatus.Running,
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            PlannedDurationMinutes = 25
        };

        var verificationResult = new VerificationResult
        {
            IsPass = true,
            Score = 80,
            Feedback = "Good work!"
        };

        SetupFocusSessionsDbSet(new List<FocusSession> { session });
        _mockVerificationService.Setup(x => x.VerifyCodeSubmissionAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(verificationResult);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(SessionStatus.CompletedEarly.ToString(), result.Value.SessionStatus);
        Assert.Contains("early", result.Value.Message);
    }

    [Fact]
    public async Task Handle_WithSessionNotFound_ShouldReturnFailure()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var command = new CompleteSessionCommand(sessionId, "code", null, null, false, CodeNexus.Domain.Enums.SubmissionType.Progress);

        SetupFocusSessionsDbSet(new List<FocusSession>());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("SESSION_NOT_FOUND", result.ErrorCode);
        Assert.Contains("Session not found", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WithSessionNotRunning_ShouldReturnFailure()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var command = new CompleteSessionCommand(sessionId, "code", null, null, false, CodeNexus.Domain.Enums.SubmissionType.Progress);

        var task = new TaskEntity
        {
            TaskId = taskId,
            Title = "Practice Task",
            TaskType = TaskType.Practice,
            LearningPath = BuildLearningPath()
        };
        task.PathId = task.LearningPath.PathId;

        var session = new FocusSession
        {
            SessionId = sessionId,
            TaskId = taskId,
            Task = task,
            SessionStatus = SessionStatus.CompletedOnTime, // Not running
            StartTime = DateTime.UtcNow.AddMinutes(-25),
            PlannedDurationMinutes = 25
        };

        SetupFocusSessionsDbSet(new List<FocusSession> { session });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("SESSION_NOT_RUNNING", result.ErrorCode);
        Assert.Contains("Session is not running", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WithMissingCodeSubmission_ShouldReturnFailure()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var command = new CompleteSessionCommand(sessionId, null, null, null, false, CodeNexus.Domain.Enums.SubmissionType.Final); // No code for practice task

        var task = new TaskEntity
        {
            TaskId = taskId,
            Title = "Practice Task",
            TaskType = TaskType.Practice,
            Status = TaskStatus_.InProgress,
            LearningPath = BuildLearningPath()
        };
        task.PathId = task.LearningPath.PathId;

        var session = new FocusSession
        {
            SessionId = sessionId,
            TaskId = taskId,
            Task = task,
            SessionStatus = SessionStatus.Running,
            StartTime = DateTime.UtcNow.AddMinutes(-20),
            PlannedDurationMinutes = 25
        };

        SetupFocusSessionsDbSet(new List<FocusSession> { session });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("MISSING_CODE_SUBMISSION", result.ErrorCode);
        Assert.Contains("Code submission is required for coding tasks", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WithMissingSummarySubmission_ShouldReturnFailure()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var command = new CompleteSessionCommand(sessionId, null, null, null, false, CodeNexus.Domain.Enums.SubmissionType.Final); // No summary for theory task

        var task = new TaskEntity
        {
            TaskId = taskId,
            Title = "Theory Task",
            TaskType = TaskType.Theory,
            Status = TaskStatus_.InProgress,
            LearningPath = BuildLearningPath()
        };
        task.PathId = task.LearningPath.PathId;

        var session = new FocusSession
        {
            SessionId = sessionId,
            TaskId = taskId,
            Task = task,
            SessionStatus = SessionStatus.Running,
            StartTime = DateTime.UtcNow.AddMinutes(-20),
            PlannedDurationMinutes = 25
        };

        SetupFocusSessionsDbSet(new List<FocusSession> { session });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("MISSING_SUMMARY_SUBMISSION", result.ErrorCode);
        Assert.Contains("Summary submission is required for summary tasks", result.ErrorMessage);
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

    private static LearningPath BuildLearningPath()
    {
        var subjectId = Guid.NewGuid();
        return new LearningPath
        {
            PathId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            SubjectId = subjectId,
            Subject = new Subject
            {
                SubjectId = subjectId,
                Name = "Test Subject"
            },
            Title = "Test Learning Path",
            CreatedByType = false
        };
    }
}
