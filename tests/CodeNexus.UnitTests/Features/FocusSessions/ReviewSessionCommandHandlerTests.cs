using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.FocusSessions.Commands.ReviewSession;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using TaskEntity = CodeNexus.Domain.Entities.Tasks;

namespace CodeNexus.UnitTests.Features.FocusSessions;

public class ReviewSessionCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ITaskVerificationService> _mockVerificationService;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<IPlanUsageLimitService> _mockPlanUsageLimitService;
    private readonly ReviewSessionCommandHandler _handler;

    public ReviewSessionCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockVerificationService = new Mock<ITaskVerificationService>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockPlanUsageLimitService = new Mock<IPlanUsageLimitService>();
        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(Guid.NewGuid());
        _mockPlanUsageLimitService.Setup(x => x.CheckFocusSessionReviewAllowedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CodeNexus.Application.Common.Models.Result.Success());
        _mockPlanUsageLimitService.Setup(x => x.RecordFocusSessionReviewUsageAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _handler = new ReviewSessionCommandHandler(
            _mockContext.Object,
            _mockVerificationService.Object,
            _mockCurrentUserService.Object,
            _mockPlanUsageLimitService.Object);
    }

    [Fact]
    public async Task Handle_WithValidPracticeTask_ShouldReturnFeedbackSuccessfully()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var command = new ReviewSessionCommand(sessionId, "console.log('Hello World');", null);

        var task = new TaskEntity
        {
            TaskId = taskId,
            Title = "Practice Task",
            Description = "Write a hello world program",
            TaskType = TaskType.Practice,
            Status = TaskStatus_.InProgress,
            VerificationPrompt = "Check if code prints hello world"
        };

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
            Feedback = "Great job! Your code correctly prints 'Hello World'."
        };

        var mockDbSet = new List<FocusSession> { session }.AsQueryable().BuildMockDbSet();
        _mockContext.Setup(c => c.FocusSessions).Returns(mockDbSet.Object);
        _mockVerificationService.Setup(v => v.VerifyCodeSubmissionAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(verificationResult);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(sessionId, result.Value.SessionId);
        Assert.Equal("Great job! Your code correctly prints 'Hello World'.", result.Value.AIFeedback);
        Assert.Equal(85, result.Value.VerificationScore);
        Assert.Equal("Code reviewed successfully", result.Value.Message);
    }

    [Fact]
    public async Task Handle_WithValidTheoryTask_ShouldReturnFeedbackSuccessfully()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var command = new ReviewSessionCommand(sessionId, null, "This is a summary of the lesson");

        var task = new TaskEntity
        {
            TaskId = taskId,
            Title = "Theory Task",
            Description = "Write a summary of the lesson",
            TaskType = TaskType.Theory,
            Status = TaskStatus_.InProgress,
            VerificationPrompt = "Check if summary covers key points"
        };

        var session = new FocusSession
        {
            SessionId = sessionId,
            TaskId = taskId,
            Task = task,
            SessionStatus = SessionStatus.Running,
            StartTime = DateTime.UtcNow.AddMinutes(-15),
            PlannedDurationMinutes = 30
        };

        var verificationResult = new VerificationResult
        {
            IsPass = true,
            Score = 90,
            Feedback = "Excellent summary! You covered all the key points."
        };

        var mockDbSet = new List<FocusSession> { session }.AsQueryable().BuildMockDbSet();
        _mockContext.Setup(c => c.FocusSessions).Returns(mockDbSet.Object);
        _mockVerificationService.Setup(v => v.VerifySummarySubmissionAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(verificationResult);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(sessionId, result.Value.SessionId);
        Assert.Equal("Excellent summary! You covered all the key points.", result.Value.AIFeedback);
        Assert.Equal(90, result.Value.VerificationScore);
        Assert.Equal("Code reviewed successfully", result.Value.Message);
    }

    [Fact]
    public async Task Handle_WithNonExistentSession_ShouldReturnFailure()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var command = new ReviewSessionCommand(sessionId, "console.log('Hello');", null);

        var mockDbSet = new List<FocusSession>().AsQueryable().BuildMockDbSet();
        _mockContext.Setup(c => c.FocusSessions).Returns(mockDbSet.Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("SESSION_NOT_FOUND", result.ErrorCode);
        Assert.Equal("Session not found", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WithNonRunningSession_ShouldReturnFailure()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var command = new ReviewSessionCommand(sessionId, "console.log('Hello');", null);

        var task = new TaskEntity
        {
            TaskId = taskId,
            Title = "Practice Task",
            TaskType = TaskType.Practice
        };

        var session = new FocusSession
        {
            SessionId = sessionId,
            TaskId = taskId,
            Task = task,
            SessionStatus = SessionStatus.CompletedOnTime,
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            EndTime = DateTime.UtcNow.AddMinutes(-5),
            PlannedDurationMinutes = 25
        };

        var mockDbSet = new List<FocusSession> { session }.AsQueryable().BuildMockDbSet();
        _mockContext.Setup(c => c.FocusSessions).Returns(mockDbSet.Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("SESSION_NOT_RUNNING", result.ErrorCode);
        Assert.Equal("Session is not currently running", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WithPracticeTaskButNoCode_ShouldReturnFailure()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var command = new ReviewSessionCommand(sessionId, null, null);

        var task = new TaskEntity
        {
            TaskId = taskId,
            Title = "Practice Task",
            TaskType = TaskType.Practice
        };

        var session = new FocusSession
        {
            SessionId = sessionId,
            TaskId = taskId,
            Task = task,
            SessionStatus = SessionStatus.Running,
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            PlannedDurationMinutes = 25
        };

        var mockDbSet = new List<FocusSession> { session }.AsQueryable().BuildMockDbSet();
        _mockContext.Setup(c => c.FocusSessions).Returns(mockDbSet.Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("MISSING_CODE_SUBMISSION", result.ErrorCode);
        Assert.Equal("Code submission is required for coding tasks", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WithTheoryTaskButNoSummary_ShouldReturnFailure()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var command = new ReviewSessionCommand(sessionId, null, null);

        var task = new TaskEntity
        {
            TaskId = taskId,
            Title = "Theory Task",
            TaskType = TaskType.Theory
        };

        var session = new FocusSession
        {
            SessionId = sessionId,
            TaskId = taskId,
            Task = task,
            SessionStatus = SessionStatus.Running,
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            PlannedDurationMinutes = 25
        };

        var mockDbSet = new List<FocusSession> { session }.AsQueryable().BuildMockDbSet();
        _mockContext.Setup(c => c.FocusSessions).Returns(mockDbSet.Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("MISSING_SUMMARY_SUBMISSION", result.ErrorCode);
        Assert.Equal("Summary submission is required for summary tasks", result.ErrorMessage);
    }

    [Fact]
    public async Task Handle_WithVerificationServiceException_ShouldReturnSuccessWithErrorMessage()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var command = new ReviewSessionCommand(sessionId, "console.log('Hello');", null);

        var task = new TaskEntity
        {
            TaskId = taskId,
            Title = "Practice Task",
            TaskType = TaskType.Practice
        };

        var session = new FocusSession
        {
            SessionId = sessionId,
            TaskId = taskId,
            Task = task,
            SessionStatus = SessionStatus.Running,
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            PlannedDurationMinutes = 25
        };

        var mockDbSet = new List<FocusSession> { session }.AsQueryable().BuildMockDbSet();
        _mockContext.Setup(c => c.FocusSessions).Returns(mockDbSet.Object);
        _mockVerificationService.Setup(v => v.VerifyCodeSubmissionAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("Verification service unavailable"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Unable to generate feedback at this time. Please try again later.", result.Value.AIFeedback);
        Assert.Null(result.Value.VerificationScore);
    }
}
