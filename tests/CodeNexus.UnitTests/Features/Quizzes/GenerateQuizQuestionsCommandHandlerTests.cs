using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Quizzes.Commands.GenerateQuizQuestions;
using CodeNexus.Application.Features.Quizzes.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.Quizzes;

public class GenerateQuizQuestionsCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<IAIGeneratorService> _mockAIGeneratorService;
    private readonly GenerateQuizQuestionsCommandHandler _handler;

    public GenerateQuizQuestionsCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockAIGeneratorService = new Mock<IAIGeneratorService>();

        _handler = new GenerateQuizQuestionsCommandHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object,
            _mockAIGeneratorService.Object
        );
    }

    [Fact]
    public async Task Handle_QuizNotFound_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var command = new GenerateQuizQuestionsCommand(NewId.NextGuid());

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
        var command = new GenerateQuizQuestionsCommand(quizId);

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
        var command = new GenerateQuizQuestionsCommand(NewId.NextGuid());

        var quiz = CreateQuizGraph(command.QuizId, otherUserId);

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
    public async Task Handle_QuestionsAlreadyExist_ReturnsExisting()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var command = new GenerateQuizQuestionsCommand(NewId.NextGuid());

        var quiz = CreateQuizGraph(command.QuizId, userId);
        quiz.Questions = new List<Questions>
        {
            new Questions
            {
                QuestionId = NewId.NextGuid(),
                QuizId = quiz.QuizId,
                QuestionText = "What is a variable?",
                Type = QuestionType.MultipleChoice,
                Options = "A container||A function||A loop||A class",
                CorrectAnswer = "A container",
                Points = 1,
                OrderIndex = 0
            }
        };

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Quizzes).Returns(
            new[] { quiz }.BuildMockDbSet().Object);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.QuizId.Should().Be(quiz.QuizId);
        result.Value.Questions.Should().HaveCount(1);
        result.Value.Questions[0].QuestionText.Should().Be("What is a variable?");
        _mockAIGeneratorService.Verify(
            x => x.GenerateStructureAsync<GeneratedQuestionsDto>(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NoQuestionsYet_GeneratesSavesAndReturns()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var command = new GenerateQuizQuestionsCommand(NewId.NextGuid());

        var quiz = CreateQuizGraph(command.QuizId, userId);

        var generated = new GeneratedQuestionsDto(new List<GeneratedQuestionDto>
        {
            new("What is a variable?", QuestionType.MultipleChoice,
                new List<string> { "A container", "A function", "A loop", "A class" },
                "A container", 1),
            new("Python is a compiled language.", QuestionType.TrueFalse,
                new List<string> { "True", "False" },
                "False", 1)
        });

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Quizzes).Returns(
            new[] { quiz }.BuildMockDbSet().Object);
        _mockAIGeneratorService.Setup(x => x.GenerateStructureAsync<GeneratedQuestionsDto>(It.IsAny<string>()))
            .ReturnsAsync(generated);

        var questionsList = new List<Questions>();
        _mockContext.Setup(x => x.Questions).Returns(
            questionsList.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.QuizId.Should().Be(quiz.QuizId);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AIReturnsEmptyQuestions_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var command = new GenerateQuizQuestionsCommand(NewId.NextGuid());

        var quiz = CreateQuizGraph(command.QuizId, userId);

        var generated = new GeneratedQuestionsDto(new List<GeneratedQuestionDto>());

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Quizzes).Returns(
            new[] { quiz }.BuildMockDbSet().Object);
        _mockAIGeneratorService.Setup(x => x.GenerateStructureAsync<GeneratedQuestionsDto>(It.IsAny<string>()))
            .ReturnsAsync(generated);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("INVALID_AI_RESPONSE");
    }

    [Fact]
    public async Task Handle_AIGenerationFails_ReturnsFailure()
    {
        // Arrange
        var userId = NewId.NextGuid();
        var command = new GenerateQuizQuestionsCommand(NewId.NextGuid());

        var quiz = CreateQuizGraph(command.QuizId, userId);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Quizzes).Returns(
            new[] { quiz }.BuildMockDbSet().Object);
        _mockAIGeneratorService.Setup(x => x.GenerateStructureAsync<GeneratedQuestionsDto>(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Groq API timeout"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("QUESTION_GENERATION_FAILED");
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Quiz CreateQuizGraph(Guid quizId, Guid userId)
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
            Content = "## Overview\nVariables in Python are containers for storing data values...",
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
            Description = "Test your knowledge of Python variables",
            Questions = new List<Questions>()
        };

        return quiz;
    }
}
