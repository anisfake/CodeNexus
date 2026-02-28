using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Tasks.DTOs;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Tasks.Commands.GenerateChapterTasks;

public class GenerateChapterTasksCommandHandler : IRequestHandler<GenerateChapterTasksCommand, Result<ChapterTasksDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAIGeneratorService _aiGeneratorService;

    public GenerateChapterTasksCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IAIGeneratorService aiGeneratorService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _aiGeneratorService = aiGeneratorService;
    }

    public async Task<Result<ChapterTasksDto>> Handle(GenerateChapterTasksCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var chapter = await _context.Chapters
                .Include(c => c.LearningPath)
                    .ThenInclude(lp => lp.Subject)
                .Include(c => c.Lessons)
                .Include(c => c.Tasks)
            .FirstOrDefaultAsync(c => c.ChapterId == request.ChapterId, cancellationToken);

        if (chapter == null)
            return Result<ChapterTasksDto>.Failure("CHAPTER_NOT_FOUND", "Chapter not found");

        if (chapter.LearningPath.UserId != userId)
            return Result<ChapterTasksDto>.Failure("UNAUTHORIZED", "You do not have access to this chapter");

        if (!chapter.Lessons.Any())
            return Result<ChapterTasksDto>.Failure("CHAPTER_NO_LESSONS", "Chapter has no lessons to generate tasks from");

        if (chapter.Tasks.Any())
        {
            return Result<ChapterTasksDto>.Success(MapToDto(chapter));
        }

        try
        {
            var prompt = BuildPrompt(chapter);
            var generated = await _aiGeneratorService.GenerateStructureAsync<GeneratedTasksDto>(prompt);

            if (generated?.Tasks == null || generated.Tasks.Count == 0)
                return Result<ChapterTasksDto>.Failure("INVALID_AI_RESPONSE", "AI returned no tasks");

            foreach (var t in generated.Tasks)
            {
                var task = new Domain.Entities.Tasks
                {
                    TaskId = NewId.NextGuid(),
                    ChapterId = chapter.ChapterId,
                    PathId = chapter.PathId,
                    Title = t.Title,
                    Description = t.Description,
                    Priority = ParsePriority(t.Priority),
                    Status = TaskStatus_.Pending,
                    CreatedAt = DateTime.Now
                };

                await _context.Tasks.AddAsync(task, cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);

            var savedChapter = await _context.Chapters
                    .Include(c => c.Tasks)
                .FirstAsync(c => c.ChapterId == request.ChapterId, cancellationToken);

            return Result<ChapterTasksDto>.Success(MapToDto(savedChapter));
        }
        catch (Exception ex)
        {
            return Result<ChapterTasksDto>.Failure("TASK_GENERATION_FAILED",
                $"Failed to generate chapter tasks: {ex.Message}");
        }
    }

    private static ChapterTasksDto MapToDto(Domain.Entities.Chapter chapter)
    {
        var tasks = chapter.Tasks
            .Select(t => new TaskItemDto(
                t.TaskId,
                t.Title,
                t.Description,
                t.Priority,
                t.Status
            ))
            .ToList();

        return new ChapterTasksDto(chapter.ChapterId, chapter.Title, tasks);
    }

    private static TaskPriority ParsePriority(string priority)
    {
        return priority?.ToLowerInvariant() switch
        {
            "high" => TaskPriority.High,
            "medium" => TaskPriority.Medium,
            _ => TaskPriority.Low
        };
    }

    private static string BuildPrompt(Domain.Entities.Chapter chapter)
    {
        var learningPath = chapter.LearningPath;
        var subject = learningPath.Subject.Name;

        var lessonTitles = chapter.Lessons
            .OrderBy(l => l.OrderIndex)
            .Select(l => l.Title);
        var lessons = string.Join("\n- ", lessonTitles);

        return $@"You are a study planning assistant for a {subject} course.

=== CONTEXT ===
Subject: {subject}
Learning path title: {learningPath.Title}
Learning path description: {learningPath.Description ?? "N/A"}
Chapter title: {chapter.Title}
Chapter description: {chapter.Content ?? "N/A"}
Lessons in this chapter:
- {lessons}

=== TASK ===
Based on the lessons listed above in this chapter, generate practical study tasks that a student should complete.
Each task should correspond to one or more lessons and help the student practice or apply what they learned.

=== REQUIREMENTS ===
- Generate between 2 and 4 tasks depending on the number and complexity of lessons
- Each task must have a clear, actionable title
- Each task must have a description explaining what the student should do
- Priority: ""Low"", ""Medium"", or ""High"" based on difficulty
- Write tasks in the same language as the chapter title
- Tasks should cover all lessons in the chapter

Return ONLY valid JSON (no markdown, no extra text):
{{
  ""tasks"": [
    {{
      ""title"": ""Practice basic syntax"",
      ""description"": ""Write small programs to practice the basic syntax covered in the introductory lessons."",
      ""priority"": ""High""
    }},
    {{
      ""title"": ""Solve exercises on control flow"",
      ""description"": ""Complete exercises involving if-else statements, loops, and switch cases."",
      ""priority"": ""Medium""
    }}
  ]
}}";
    }
}
