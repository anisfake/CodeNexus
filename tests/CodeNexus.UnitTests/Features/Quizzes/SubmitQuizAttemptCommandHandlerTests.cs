using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Quizzes.Commands.SubmitQuizAttempt;
using CodeNexus.Application.Features.Quizzes.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.Quizzes;

public class SubmitQuizAttemptCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly SubmitQuizAttemptCommandHandler _handler;

    public SubmitQuizAttemptCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockContext.Setup(x => x.DailyCheckins).Returns(new List<DailyCheckins>().BuildMockDbSet().Object);

        _handler = new SubmitQuizAttemptCommandHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object
        );
    }

    [Fact]
    public async Task Handle_AttemptNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var command = new SubmitQuizAttemptCommand(NewId.NextGuid(), new List<AnswerItemDto>());

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.QuizAttempts).Returns(
            new List<QuizAttempt>().BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("ATTEMPT_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_UnauthorizedUser_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var otherUserId = NewId.NextGuid();
        var attempt = CreateAttemptWithQuiz(otherUserId);
        var command = new SubmitQuizAttemptCommand(attempt.AttemptId, new List<AnswerItemDto>());

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.QuizAttempts).Returns(
            new[] { attempt }.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task Handle_AlreadyCompleted_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var attempt = CreateAttemptWithQuiz(userId);
        attempt.Status = QuizAttemptStatus.NotPassed;
        var command = new SubmitQuizAttemptCommand(attempt.AttemptId, new List<AnswerItemDto>());

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.QuizAttempts).Returns(
            new[] { attempt }.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("ATTEMPT_ALREADY_COMPLETED");
    }

    [Fact]
    public async Task Handle_CorrectAnswers_ReturnsFullScore()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var attempt = CreateAttemptWithQuiz(userId);
        var questions = attempt.Quiz.Questions.ToList();

        var answers = new List<AnswerItemDto>
        {
            new(questions[0].QuestionId, "False"),
            new(questions[1].QuestionId, "def")
        };
        var command = new SubmitQuizAttemptCommand(attempt.AttemptId, answers);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.QuizAttempts).Returns(
            new[] { attempt }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Score.Should().Be(10);
        result.Value.TotalPoints.Should().Be(10);
        result.Value.Percentage.Should().Be(100);
        result.Value.Passed.Should().BeTrue();
        result.Value.QuestionResults.Should().HaveCount(2);
        result.Value.QuestionResults.Should().AllSatisfy(qr => qr.IsCorrect.Should().BeTrue());
    }

    [Fact]
    public async Task Handle_WrongAnswers_ReturnsZeroScore()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var attempt = CreateAttemptWithQuiz(userId);
        var questions = attempt.Quiz.Questions.ToList();

        var answers = new List<AnswerItemDto>
        {
            new(questions[0].QuestionId, "True"),
            new(questions[1].QuestionId, "func")
        };
        var command = new SubmitQuizAttemptCommand(attempt.AttemptId, answers);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.QuizAttempts).Returns(
            new[] { attempt }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Score.Should().Be(0);
        result.Value.Passed.Should().BeFalse();
        result.Value.QuestionResults.Should().AllSatisfy(qr => qr.IsCorrect.Should().BeFalse());
    }

    [Fact]
    public async Task Handle_TimeExpired_AllAnswersWrong()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var attempt = CreateAttemptWithQuiz(userId);
        attempt.StartTime = DateTime.UtcNow.AddMinutes(-60);
        var questions = attempt.Quiz.Questions.ToList();

        var answers = new List<AnswerItemDto>
        {
            new(questions[0].QuestionId, "False"),
            new(questions[1].QuestionId, "def")
        };
        var command = new SubmitQuizAttemptCommand(attempt.AttemptId, answers);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.QuizAttempts).Returns(
            new[] { attempt }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Score.Should().Be(0);
        result.Value.Passed.Should().BeFalse();
        result.Value.QuestionResults.Should().AllSatisfy(qr => qr.IsCorrect.Should().BeFalse());
    }

    [Fact]
    public async Task Handle_PartialAnswers_ReturnsPartialScore()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var attempt = CreateAttemptWithQuiz(userId);
        var questions = attempt.Quiz.Questions.ToList();

        var answers = new List<AnswerItemDto>
        {
            new(questions[0].QuestionId, "False"),
            new(questions[1].QuestionId, "func")
        };
        var command = new SubmitQuizAttemptCommand(attempt.AttemptId, answers);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.QuizAttempts).Returns(
            new[] { attempt }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Score.Should().Be(4.0m);
        result.Value.TotalPoints.Should().Be(10);
        result.Value.Percentage.Should().Be(40);
        result.Value.Passed.Should().BeFalse();
    }

    private static QuizAttempt CreateAttemptWithQuiz(Guid userId)
    {
        var quizId = NewId.NextGuid();

        var quiz = new Quiz
        {
            QuizId = quizId,
            Title = "Variables Quiz",
            Description = "Test your knowledge",
            TimeLimit = 8,
            PassingScore = 8,
            Questions = new List<Questions>
            {
                new Questions
                {
                    QuestionId = NewId.NextGuid(),
                    QuizId = quizId,
                    QuestionText = "Python is compiled?",
                    Type = QuestionType.TrueFalse,
                    Options = "True||False",
                    CorrectAnswer = "False",
                    Points = 4.0m,
                    OrderIndex = 0
                },
                new Questions
                {
                    QuestionId = NewId.NextGuid(),
                    QuizId = quizId,
                    QuestionText = "What keyword defines a function?",
                    Type = QuestionType.SingleChoice,
                    Options = "func||def||function||define",
                    CorrectAnswer = "def",
                    Points = 6.0m,
                    OrderIndex = 1
                }
            }
        };

        return new QuizAttempt
        {
            AttemptId = NewId.NextGuid(),
            QuizId = quizId,
            Quiz = quiz,
            UserId = userId,
            StartTime = DateTime.UtcNow.AddMinutes(-2),
            Status = QuizAttemptStatus.InProgress
        };
    }
}
