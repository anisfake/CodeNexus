using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Features.Quizzes.Commands.GenerateSingleQuizQuestion;
using CodeNexus.Application.Features.Quizzes.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using CodeNexus.UnitTests.Helpers;
using FluentAssertions;
using MassTransit;
using Moq;
using Xunit;

namespace CodeNexus.UnitTests.Features.Quizzes;

public class GenerateSingleQuizQuestionCommandHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly Mock<ICurrentUserService> _mockCurrentUserService;
    private readonly Mock<IAIGeneratorService> _mockAIGeneratorService;
    private readonly GenerateSingleQuizQuestionCommandHandler _handler;

    public GenerateSingleQuizQuestionCommandHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        _mockCurrentUserService = new Mock<ICurrentUserService>();
        _mockAIGeneratorService = new Mock<IAIGeneratorService>();

        _handler = new GenerateSingleQuizQuestionCommandHandler(
            _mockContext.Object,
            _mockCurrentUserService.Object,
            _mockAIGeneratorService.Object);
    }

    [Fact]
    public async Task Handle_QuizNotFound_ReturnsFailure()
    {
        var userId = NewId.NextGuid();
        var command = new GenerateSingleQuizQuestionCommand(NewId.NextGuid(), QuestionType.SingleChoice);

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Quizzes).Returns(new List<Quiz>().BuildMockDbSet().Object);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("QUIZ_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_DuplicateQuestionAcrossLesson_ReturnsFailure()
    {
        var userId = NewId.NextGuid();
        var quizId = NewId.NextGuid();
        var command = new GenerateSingleQuizQuestionCommand(quizId, QuestionType.SingleChoice);

        var quiz = CreateQuizGraph(quizId, userId);
        var sameLessonOtherQuiz = new Quiz
        {
            QuizId = NewId.NextGuid(),
            LessonId = quiz.LessonId,
            Lesson = quiz.Lesson,
            Title = "Loop Quiz 2",
            Description = "More loops",
            Questions = new List<Questions>
            {
                new()
                {
                    QuestionId = NewId.NextGuid(),
                    QuizId = NewId.NextGuid(),
                    QuestionText = "What is a for loop in C#?",
                    Type = QuestionType.SingleChoice,
                    Options = "A||B||C||D",
                    CorrectAnswer = "A",
                    Points = 1,
                    OrderIndex = 0
                }
            }
        };

        var generated = new GeneratedQuestionsDto(8, new List<GeneratedQuestionDto>
        {
            new("What is a for loop in C#?", QuestionType.SingleChoice, new List<string>{"A","B","C","D"}, "A", 1)
        });

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Quizzes).Returns(new[] { quiz, sameLessonOtherQuiz }.BuildMockDbSet().Object);
        _mockAIGeneratorService.Setup(x => x.GenerateStructureAsync<GeneratedQuestionsDto>(It.IsAny<string>(), It.IsAny<AIUsageType>()))
            .ReturnsAsync(generated);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("DUPLICATE_QUESTION");
    }

    [Fact]
    public async Task Handle_ValidQuestion_GeneratesAndSaves()
    {
        var userId = NewId.NextGuid();
        var quizId = NewId.NextGuid();
        var command = new GenerateSingleQuizQuestionCommand(quizId, QuestionType.SingleChoice);

        var quiz = CreateQuizGraph(quizId, userId);
        quiz.Questions = new List<Questions>
        {
            new()
            {
                QuestionId = NewId.NextGuid(),
                QuizId = quizId,
                QuestionText = "Existing question",
                Type = QuestionType.TrueFalse,
                Options = "True||False",
                CorrectAnswer = "True",
                Points = 1,
                OrderIndex = 0
            }
        };

        var generated = new GeneratedQuestionsDto(8, new List<GeneratedQuestionDto>
        {
            new("Which statement about while loop is correct?", QuestionType.SingleChoice, new List<string>{"A","B","C","D"}, "B", 1.5m)
        });

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Quizzes).Returns(new[] { quiz }.BuildMockDbSet().Object);
        _mockAIGeneratorService.Setup(x => x.GenerateStructureAsync<GeneratedQuestionsDto>(It.IsAny<string>(), It.IsAny<AIUsageType>()))
            .ReturnsAsync(generated);
        var questions = new List<Questions>();
        _mockContext.Setup(x => x.Questions).Returns(questions.BuildMockDbSet().Object);
        _mockContext.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.QuestionText.Should().Be("Which statement about while loop is correct?");
        result.Value.OrderIndex.Should().Be(1);
        _mockContext.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AIReturnsDifferentType_ReturnsFailure()
    {
        var userId = NewId.NextGuid();
        var quizId = NewId.NextGuid();
        var command = new GenerateSingleQuizQuestionCommand(quizId, QuestionType.FillInTheBlank);

        var quiz = CreateQuizGraph(quizId, userId);

        var generated = new GeneratedQuestionsDto(8, new List<GeneratedQuestionDto>
        {
            new("Which statement about while loop is correct?", QuestionType.SingleChoice, new List<string>{"A","B","C","D"}, "B", 1.5m)
        });

        _mockCurrentUserService.Setup(x => x.GetUserId()).Returns(userId);
        _mockContext.Setup(x => x.Quizzes).Returns(new[] { quiz }.BuildMockDbSet().Object);
        _mockAIGeneratorService.Setup(x => x.GenerateStructureAsync<GeneratedQuestionsDto>(It.IsAny<string>(), It.IsAny<AIUsageType>()))
            .ReturnsAsync(generated);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("QUESTION_TYPE_MISMATCH");
    }

    private static Quiz CreateQuizGraph(Guid quizId, Guid userId)
    {
        var subject = new Subject
        {
            SubjectId = NewId.NextGuid(),
            Name = "C#",
            CreatedByUserId = userId
        };

        var learningPath = new LearningPath
        {
            PathId = NewId.NextGuid(),
            UserId = userId,
            SubjectId = subject.SubjectId,
            Subject = subject,
            Title = "C# Path",
            Description = "Learn C#"
        };

        var chapter = new Chapter
        {
            ChapterId = NewId.NextGuid(),
            PathId = learningPath.PathId,
            LearningPath = learningPath,
            Title = "Loops",
            Content = "Loop chapter"
        };

        var lesson = new Lesson
        {
            LessonId = NewId.NextGuid(),
            ChapterId = chapter.ChapterId,
            Chapter = chapter,
            Title = "For and While",
            Content = "for loop, while loop"
        };

        return new Quiz
        {
            QuizId = quizId,
            LessonId = lesson.LessonId,
            Lesson = lesson,
            Title = "Loop Basics",
            Description = "Basic loops",
            Questions = new List<Questions>()
        };
    }
}
