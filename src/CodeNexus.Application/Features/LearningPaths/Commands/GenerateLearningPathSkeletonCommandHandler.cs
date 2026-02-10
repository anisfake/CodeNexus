using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.Commands.GenerateLearningPathSkeleton;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Entities;
using MediatR;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPaths.Commands.GenerateLearningPathSkeleton;

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

            var subject = await _context.Subjects.FindAsync(new object[] { request.SubjectId }, cancellationToken: cancellationToken);
            if (subject == null)
            {
                return Result<CreateLearningPathResponse>.Failure("SUBJECT_NOT_FOUND", "Subject not found");
            }

            var goal = await _context.Goals.FindAsync(new object[] { request.GoalId }, cancellationToken: cancellationToken);
            if (goal == null)
            {
                return Result<CreateLearningPathResponse>.Failure("GOAL_NOT_FOUND", "Goal not found");
            }

            var (chapterCount, lessonsPerChapter, tasksPerChapter, quizzPercentage) = CalculateStructure(goal.DurationDays);

            LearningPathSkeletonDto skeleton;
            try
            {
                skeleton = await GenerateLearningPathSkeletonFromAI(
                    subject.Name,
                    goal.Title,
                    goal.Description,
                    chapterCount,
                    lessonsPerChapter,
                    tasksPerChapter,
                    quizzPercentage);
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
                EndDate = DateTime.Now.AddDays(goal.DurationDays),
                CreatedAt = DateTime.Now,
                CreatedByType = true
            };

            await _context.LearningPaths.AddAsync(learningPath, cancellationToken);

            foreach (var chapterDto in skeleton.Chapters)
            {
                var chapter = new Chapter
                {
                    ChapterId = NewId.NextGuid(),
                    PathId = learningPath.PathId,
                    Title = chapterDto.Title,
                    Content = chapterDto.Description,
                    OrderIndex = chapterDto.OrderIndex,
                    IsCompleted = false,
                    CreatedAt = DateTime.Now
                };

                await _context.Chapters.AddAsync(chapter, cancellationToken);

                foreach (var lessonDto in chapterDto.Lessons)
                {
                    var lesson = new Lesson
                    {
                        LessonId = NewId.NextGuid(),
                        ChapterId = chapter.ChapterId,
                        Title = lessonDto.Title,
                        Content = lessonDto.Description ?? string.Empty,
                        OrderIndex = chapterDto.Lessons.IndexOf(lessonDto),
                        CreatedAt = DateTime.Now
                    };

                    await _context.Lessons.AddAsync(lesson, cancellationToken);

                    foreach (var quizDto in lessonDto.Quizzes)
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

                foreach (var taskDto in chapterDto.Tasks)
                {
                    var task = new Tasks
                    {
                        TaskId = NewId.NextGuid(),
                        ChapterId = chapter.ChapterId,
                        PathId = learningPath.PathId,
                        Title = taskDto.Title,
                        Description = taskDto.Description,
                        Status = Domain.Enums.TaskStatus_.Pending,
                        CreatedAt = DateTime.Now
                    };

                    await _context.Tasks.AddAsync(task, cancellationToken);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            return Result<CreateLearningPathResponse>.Success(
                new CreateLearningPathResponse(
                    learningPath.PathId,
                    learningPath.Title,
                    learningPath.Description,
                    skeleton.Chapters.Count,
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

    private (int chapters, int lessonsPerChapter, int tasksPerChapter, int quizzPercentage) CalculateStructure(int durationDays)
    {
        if (durationDays <= 0)
            return (1, 1, 1, 50);

        var weeks = Math.Max(1, durationDays / 7);
        var chapters = Math.Min(weeks, 12);

        int lessonsPerChapter, tasksPerChapter, quizzPercentage;

        if (durationDays <= 7)
        {
            lessonsPerChapter = 1;
            tasksPerChapter = 1;
            quizzPercentage = 0;
        }
        else if (durationDays <= 14)
        {
            lessonsPerChapter = 2;
            tasksPerChapter = 1;
            quizzPercentage = 50;
        }
        else if (durationDays <= 30)
        {
            lessonsPerChapter = 2;
            tasksPerChapter = 2;
            quizzPercentage = 50;
        }
        else if (durationDays <= 60)
        {
            lessonsPerChapter = 3;
            tasksPerChapter = 2;
            quizzPercentage = 60;
        }
        else
        {
            lessonsPerChapter = 4;
            tasksPerChapter = 3;
            quizzPercentage = 70;
        }

        return (chapters, lessonsPerChapter, tasksPerChapter, quizzPercentage);
    }

    private async Task<LearningPathSkeletonDto> GenerateLearningPathSkeletonFromAI(
        string subjectName,
        string goalTitle,
        string? goalDescription,
        int chapterCount,
        int lessonsPerChapter,
        int tasksPerChapter,
        int quizzPercentage)
    {
        var prompt = BuildPrompt(subjectName, goalTitle, goalDescription, chapterCount, lessonsPerChapter, tasksPerChapter, quizzPercentage);
        var skeleton = await _aiGeneratorService.GenerateStructureAsync<LearningPathSkeletonDto>(prompt);
        return skeleton;
    }

    private string BuildPrompt(
        string subjectName,
        string goalTitle,
        string? goalDescription,
        int chapterCount,
        int lessonsPerChapter,
        int tasksPerChapter,
        int quizzPercentage)
    {
        var quizzDescription = quizzPercentage == 0
            ? "No quizzes needed"
            : $"Approximately {quizzPercentage}% of lessons should have quizzes (some lessons have quizzes, some don't)";

        return $@"Generate a learning path in JSON format.

Subject: {subjectName}
Goal: {goalTitle}

Structure Requirements:
- Exactly {chapterCount} chapters
- Each chapter must have {lessonsPerChapter} to 5 lessons (minimum {lessonsPerChapter}, maximum 5)
- Each chapter must have {tasksPerChapter} to 3 tasks (minimum {tasksPerChapter}, maximum 3)
- {quizzDescription}
- Only include quizzes for lessons that need them (not all lessons need quizzes)
- Each quiz belongs to exactly one lesson
- Provide only titles and descriptions, no content

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
      ],
      ""tasks"": [
        {{
          ""title"": ""Task Title"",
          ""description"": ""Task description""
        }}
      ]
    }}
  ]
}}";
    }
}


