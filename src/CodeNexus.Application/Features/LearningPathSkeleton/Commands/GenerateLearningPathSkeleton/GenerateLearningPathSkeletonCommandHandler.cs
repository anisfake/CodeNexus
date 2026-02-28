using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Entities;
using MassTransit;
using MediatR;
using CodeNexus.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Commands.GenerateLearningPathSkeleton;

public class GenerateLearningPathSkeletonCommandHandler : IRequestHandler<GenerateLearningPathSkeletonCommand, Result<CreateLearningPathResponse>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAIGeneratorService _aiGeneratorService;

    public GenerateLearningPathSkeletonCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IAIGeneratorService aiGeneratorService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _aiGeneratorService = aiGeneratorService;
    }

    public async Task<Result<CreateLearningPathResponse>> Handle(GenerateLearningPathSkeletonCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.GetUserId();

            var subject = await _context.Subjects.FirstOrDefaultAsync(x => x.SubjectId == request.SubjectId, cancellationToken: cancellationToken);
            if (subject == null)
            {
                return Result<CreateLearningPathResponse>.Failure("SUBJECT_NOT_FOUND", "Subject not found");
            }

            var goal = await _context.Goals.FindAsync(new object[] { request.GoalId }, cancellationToken: cancellationToken);
            if (goal == null)
            {
                return Result<CreateLearningPathResponse>.Failure("GOAL_NOT_FOUND", "Goal not found");
            }

            var (chapterCount, lessonsPerChapter, quizzPercentage, estimatedDays) = CalculateStructureByComplexity(request.ComplexityLevel);

            LearningPathSkeletonDto skeleton;
            try
            {
                skeleton = await GenerateLearningPathSkeletonFromAI(
                    subject.Name,
                    goal.Title,
                    goal.Description,
                    chapterCount,
                    lessonsPerChapter,
                    quizzPercentage,
                    request.ComplexityLevel);
            }
            catch (Exception ex)
            {
                return Result<CreateLearningPathResponse>.Failure("AI_GENERATION_FAILED", $"Failed to generate learning path: {ex.Message}");
            }

            if (skeleton == null || string.IsNullOrEmpty(skeleton.Title))
            {
                return Result<CreateLearningPathResponse>.Failure("INVALID_AI_RESPONSE", "AI returned invalid skeleton structure");
            }

            var learningPath = new LearningPath
            {
                PathId = NewId.NextGuid(),
                UserId = userId,
                SubjectId = request.SubjectId,
                GoalId = request.GoalId,
                Title = skeleton.Title,
                Description = skeleton.Description,
                Status = "Active",
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddDays(estimatedDays),
                CreatedAt = DateTime.Now,
                CreatedByType = true
            };

            await _context.LearningPaths.AddAsync(learningPath, cancellationToken);

            foreach (var chapterDto in skeleton.Chapters ?? new List<ChapterDto>())
            {
                var chapter = new Chapter
                {
                    ChapterId = NewId.NextGuid(),
                    PathId = learningPath.PathId,
                    Title = chapterDto.Title,
                    OrderIndex = chapterDto.OrderIndex,
                    IsCompleted = false,
                    CreatedAt = DateTime.Now
                };

                await _context.Chapters.AddAsync(chapter, cancellationToken);

                foreach (var lessonDto in chapterDto.Lessons ?? new List<LessonDto>())
                {
                    var lesson = new Lesson
                    {
                        LessonId = NewId.NextGuid(),
                        ChapterId = chapter.ChapterId,
                        Title = lessonDto.Title,
                        Content = string.Empty,
                        OrderIndex = chapterDto.Lessons?.IndexOf(lessonDto) ?? 0,
                        CreatedAt = DateTime.Now
                    };

                    await _context.Lessons.AddAsync(lesson, cancellationToken);

                    foreach (var quizDto in lessonDto.Quizzes ?? new List<QuizDto>())
                    {
                        var quiz = new Quiz
                        {
                            QuizId = NewId.NextGuid(),
                            LessonId = lesson.LessonId,
                            Title = quizDto.Title,
                            Description = quizDto.Description,
                            CreatedAt = DateTime.Now
                        };

                        await _context.Quizzes.AddAsync(quiz, cancellationToken);
                    }
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            return Result<CreateLearningPathResponse>.Success(
                new CreateLearningPathResponse(
                    learningPath.PathId,
                    learningPath.Title,
                    learningPath.Description,
                    learningPath.Chapters?.Select(c => new ChapterDto(
                        c.ChapterId,
                        c.Title,
                        c.Content,
                        c.OrderIndex,
                        c.Lessons?.Select(l => new LessonDto(
                            l.LessonId,
                            l.Title,
                            l.Content,
                            l.Quizzes?.Select(q => new QuizDto(q.QuizId, q.Title, q.Description)).ToList() ?? new List<QuizDto>()
                        )).ToList() ?? new List<LessonDto>(),
                        new List<TaskDto>()
                    )).ToList() ?? new List<ChapterDto>(),
                    skeleton?.Chapters?.Count,
                    learningPath.CreatedAt,
                    true
                )
            );
        }
        catch (Exception ex)
        {
            return Result<CreateLearningPathResponse>.Failure("GENERATION_FAILED", $"Failed to generate learning path skeleton: {ex.Message}");
        }
    }

    private (int chapters, int lessonsPerChapter, int quizzPercentage, int estimatedDays) CalculateStructureByComplexity(Domain.Enums.ComplexityLevel complexity)
    {
        return complexity switch
        {
            ComplexityLevel.Beginner => (
                3,     // 3 chapters
                3,     // 3 lessons/chapter = 9 lessons total
                50,    // 50% lessons có quiz
                30     // ~1 tháng
            ),
            ComplexityLevel.Intermediate => (
                5,     // 5 chapters
                4,     // 4 lessons/chapter = 20 lessons total
                60,    // 60% lessons có quiz
                60     // ~2 tháng
            ),
            ComplexityLevel.Advanced => (
                7,     // 7 chapters
                5,     // 5 lessons/chapter = 35 lessons total
                70,    // 70% lessons có quiz
                90     // ~3 tháng
            ),
            _ => (3, 3, 50, 30)        // default
        };
    }

    private async Task<LearningPathSkeletonDto> GenerateLearningPathSkeletonFromAI(
        string subjectName,
        string goalTitle,
        string? goalDescription,
        int chapterCount,
        int lessonsPerChapter,
        int quizzPercentage,
        Domain.Enums.ComplexityLevel complexity)
    {
        var complexityText = complexity switch
        {
            ComplexityLevel.Beginner => "Basic, suitable for beginners.",
            ComplexityLevel.Intermediate => "Average, suitable for those who already have a basic understanding.",
            ComplexityLevel.Advanced => "Advanced level, suitable for those who want to specialize and already have a foundational knowledge.",
            _ => "Basic, suitable for beginners."
        };

        var prompt = BuildPrompt(subjectName, goalTitle, goalDescription, chapterCount, lessonsPerChapter, quizzPercentage, complexityText);
        var skeleton = await _aiGeneratorService.GenerateStructureAsync<LearningPathSkeletonDto>(prompt);
        return skeleton;
    }

    private string BuildPrompt(
        string subjectName,
        string goalTitle,
        string? goalDescription,
        int chapterCount,
        int lessonsPerChapter,
        int quizzPercentage,
        string complexityText)
    {
        var quizzDescription = quizzPercentage == 0
            ? "No quizzes needed"
            : $"Approximately {quizzPercentage}% of lessons should have quizzes (some lessons have quizzes, some don't)";

        return $@"Generate a learning path in JSON format.

                Subject: {subjectName}
                Goal: {goalTitle}
                Complexity Level: {complexityText}

            Structure Requirements:
                - Exactly {chapterCount} chapters
                - Each chapter must have {lessonsPerChapter} to 5 lessons (minimum {lessonsPerChapter}, maximum 5)
                - {quizzDescription}
                - Only include quizzes for lessons that need them (not all lessons need quizzes)
                - Each quiz belongs to exactly one lesson
                - Provide only titles and descriptions, no content
                - Content should match the complexity level: {complexityText}

                Return ONLY valid JSON (no markdown, no extra text):
                {{
                    ""title"": ""Learning Path Title"",
                    ""description"": ""Brief description of the learning path"",
                    ""chapters"": [
                    {{
                        ""title"": ""Chapter Title"",
                        ""description"": ""Chapter description"",
                        ""orderIndex"": 0,
                        ""lessons"": [
                        {{
                            ""title"": ""Lesson Title"",
                            ""description"": ""Lesson description"",
                            ""quizzes"": [
                            {{
                                ""title"": ""Quiz Title"",
                                ""description"": ""Quiz description""
                            }}
                        ]
                    }}
                  ]
        }}
    ]
}}";
    }
}