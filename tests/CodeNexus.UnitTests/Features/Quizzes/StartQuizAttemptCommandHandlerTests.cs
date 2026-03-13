using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Quizzes.Commands.StartQuizAttempt;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.Quizzes;

public class StartQuizAttemptCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly StartQuizAttemptCommandHandler _handler;

    public StartQuizAttemptCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();

        _handler = new StartQuizAttemptCommandHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object
        );
    }

    [Fact]
    public async Task Handle_QuizNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var command = new StartQuizAttemptCommand(NewId.NextGuid());

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Quizzes).Returns(
            new List<Quiz>().BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("QUIZ_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_QuizNoLesson_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var quizId = NewId.NextGuid();
        var command = new StartQuizAttemptCommand(quizId);

        var quiz = new Quiz
        {
            QuizId = quizId,
            LessonId = null,
            Lesson = null,
            Title = "Test Quiz",
            Questions = new List<Questions>()
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Quizzes).Returns(
            new[] { quiz }.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("QUIZ_NO_LESSON");
    }

    [Fact]
    public async Task Handle_UnauthorizedUser_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var otherUserId = NewId.NextGuid();
        var command = new StartQuizAttemptCommand(NewId.NextGuid());

        var quiz = CreateQuizWithQuestions(command.QuizId, otherUserId);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Quizzes).Returns(
            new[] { quiz }.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("UNAUTHORIZED");
    }

    [Fact]
    public async Task Handle_QuizNoQuestions_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var command = new StartQuizAttemptCommand(NewId.NextGuid());

        var quiz = CreateQuizWithQuestions(command.QuizId, userId);
        quiz.Questions = new List<Questions>();

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Quizzes).Returns(
            new[] { quiz }.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("QUIZ_NO_QUESTIONS");
    }

    [Fact]
    public async Task Handle_NoExistingAttempt_CreatesNewAttempt()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var command = new StartQuizAttemptCommand(NewId.NextGuid());

        var quiz = CreateQuizWithQuestions(command.QuizId, userId);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Quizzes).Returns(
            new[] { quiz }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.QuizAttempts).Returns(
            new List<QuizAttempt>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.QuizId.Should().Be(quiz.QuizId);
        result.Value.Title.Should().Be(quiz.Title);
        result.Value.TimeLimit.Should().Be(quiz.TimeLimit);
        result.Value.PassingScore.Should().Be(quiz.PassingScore);
        result.Value.RemainingSeconds.Should().Be((quiz.TimeLimit ?? 0) * 60);
        result.Value.Questions.Should().HaveCount(2);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingInProgressAttempt_ResumesAttempt()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var command = new StartQuizAttemptCommand(NewId.NextGuid());

        var quiz = CreateQuizWithQuestions(command.QuizId, userId);

        var existingAttempt = new QuizAttempt
        {
            AttemptId = NewId.NextGuid(),
            QuizId = quiz.QuizId,
            UserId = userId,
            StartTime = DateTime.UtcNow.AddMinutes(-2),
            Status = QuizAttemptStatus.InProgress
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Quizzes).Returns(
            new[] { quiz }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.QuizAttempts).Returns(
            new[] { existingAttempt }.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.AttemptId.Should().Be(existingAttempt.AttemptId);
        result.Value.RemainingSeconds.Should().BeGreaterThan(0);
        result.Value.RemainingSeconds.Should().BeLessThan((quiz.TimeLimit ?? 0) * 60);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ExpiredInProgressAttempt_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var command = new StartQuizAttemptCommand(NewId.NextGuid());

        var quiz = CreateQuizWithQuestions(command.QuizId, userId);

        var expiredAttempt = new QuizAttempt
        {
            AttemptId = NewId.NextGuid(),
            QuizId = quiz.QuizId,
            UserId = userId,
            StartTime = DateTime.UtcNow.AddMinutes(-60),
            Status = QuizAttemptStatus.InProgress
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Quizzes).Returns(
            new[] { quiz }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.QuizAttempts).Returns(
            new[] { expiredAttempt }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("ATTEMPT_TIME_EXPIRED");
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_TimeLimitAndPassingScoreNull_CalculatesAndSaves()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var command = new StartQuizAttemptCommand(NewId.NextGuid());

        var quiz = CreateQuizWithQuestions(command.QuizId, userId);
        quiz.TimeLimit = null;
        quiz.PassingScore = null;

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Quizzes).Returns(
            new[] { quiz }.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.QuizAttempts).Returns(
            new List<QuizAttempt>().BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        quiz.TimeLimit.Should().Be(8);
        quiz.PassingScore.Should().Be(8);
        result.Value!.TimeLimit.Should().Be(8);
        result.Value.PassingScore.Should().Be(8);
        result.Value.RemainingSeconds.Should().Be(8 * 60);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    private static Quiz CreateQuizWithQuestions(Guid quizId, Guid userId)
    {
        var subject = new Subject
        {
            SubjectId = NewId.NextGuid(),
            Name = "Python",
            CreatedByUserId = userId
        };

        var learningPath = new LearningPath
        {
            PathId = NewId.NextGuid(),
            UserId = userId,
            SubjectId = subject.SubjectId,
            Subject = subject,
            Title = "Learn Python",
            Description = "Master Python from scratch",
            Chapters = new List<Chapter>()
        };

        var chapter = new Chapter
        {
            ChapterId = NewId.NextGuid(),
            PathId = learningPath.PathId,
            LearningPath = learningPath,
            Title = "Variables and Data Types",
            Content = "Learn about Python variables",
            OrderIndex = 0,
            Lessons = new List<Lesson>()
        };

        learningPath.Chapters = new List<Chapter> { chapter };

        var lesson = new Lesson
        {
            LessonId = NewId.NextGuid(),
            ChapterId = chapter.ChapterId,
            Chapter = chapter,
            Title = "Introduction to Variables",
            Content = "Variables in Python...",
            OrderIndex = 0,
            UpdatedAt = DateTime.Now
        };

        chapter.Lessons = new List<Lesson> { lesson };

        var quiz = new Quiz
        {
            QuizId = quizId,
            LessonId = lesson.LessonId,
            Lesson = lesson,
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

        return quiz;
    }
}
