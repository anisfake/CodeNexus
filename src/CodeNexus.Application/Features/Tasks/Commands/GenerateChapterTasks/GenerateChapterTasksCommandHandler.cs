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
            var language = chapter.LearningPath.Language;
            var prompt = BuildPrompt(chapter, language);
            var generated = await _aiGeneratorService.GenerateStructureAsync<GeneratedTasksDto>(prompt, AIUsageType.ContentGeneration);

            if (generated?.Tasks == null || generated.Tasks.Count == 0)
                return Result<ChapterTasksDto>.Failure("INVALID_AI_RESPONSE", "AI returned no tasks");

            var validTasks = generated.Tasks
                .Where(t => !IsInvalidTask(t.Title, t.Description))
                .ToList();

            if (validTasks.Count == 0)
                return Result<ChapterTasksDto>.Failure("NO_VALID_TASKS", "AI generated only invalid tasks (setup/installation). Please regenerate.");

            foreach (var t in validTasks)
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
                    CreatedAt = DateTime.Now,
                    TaskType = ParseTaskType(t.TaskType),
                    VerificationMethod = ParseVerificationMethod(t.VerificationMethod),
                    VerificationPrompt = t.VerificationPrompt,
                    MinimumScore = t.MinimumScore ?? 70,
                    QuizQuestionsJson = t.QuizQuestions != null && t.QuizQuestions.Any()
                        ? System.Text.Json.JsonSerializer.Serialize(t.QuizQuestions)
                        : null
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

    private static bool IsInvalidTask(string title, string description)
    {
        var combined = $"{title} {description}".ToLowerInvariant();

        var invalidKeywords = new[]
        {
            "install", "cài đặt", "download", "tải xuống", "setup", "thiết lập",
            "configure environment", "cấu hình môi trường", "verify installation",
            "kiểm tra cài đặt", "check version", "kiểm tra phiên bản"
        };

        return invalidKeywords.Any(keyword => combined.Contains(keyword));
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

    private static TaskType ParseTaskType(string taskType)
    {
        return taskType?.ToLowerInvariant() switch
        {
            "theory" => TaskType.Theory,
            "quizz" or "quiz" or "mixed" => TaskType.Quizz,
            _ => TaskType.Practice
        };
    }

    private static VerificationMethod ParseVerificationMethod(string method)
    {
        return method?.ToLowerInvariant() switch
        {
            "summarysubmission" => VerificationMethod.SummarySubmission,
            "quickquiz" or "hybrid" => VerificationMethod.QuickQuiz,
            _ => VerificationMethod.CodeSubmission
        };
    }

    private static string BuildPrompt(Domain.Entities.Chapter chapter, LanguageSelection language)
    {
        var learningPath = chapter.LearningPath;
        var subject = learningPath.Subject.Name;

        var lessonTitles = chapter.Lessons
            .OrderBy(l => l.OrderIndex)
            .Select(l => l.Title);
        var lessons = string.Join("\n- ", lessonTitles);

        var languageInstruction = language switch
        {
            LanguageSelection.VietNamese => @"
=== LANGUAGE REQUIREMENTS ===
- Generate ALL content in Vietnamese language
- IMPORTANT: Keep technical terms in English when translating to Vietnamese would cause confusion or change meaning
- Examples of terms to keep in English: API, REST, JSON, Docker, Kubernetes, Framework, Library, Algorithm, etc.
- Use Vietnamese for general descriptions and explanations
- Example: ""Viết hàm sắp xếp bubble sort"" (correct) instead of ""Viết hàm sắp xếp bong bóng"" (wrong)
",
            LanguageSelection.English => @"
=== LANGUAGE REQUIREMENTS ===
- Generate ALL content in English language
- Use clear, professional English
",
            _ => ""
        };

        return $@"You are a study planning assistant for a {subject} course.

=== CONTEXT ===
Subject: {subject}
Learning path title: {learningPath.Title}
Learning path description: {learningPath.Description ?? "N/A"}
Chapter title: {chapter.Title}
Chapter description: {chapter.Content ?? "N/A"}
Lessons in this chapter:
- {lessons}

{languageInstruction}

=== TASK ===
Based on the lessons listed above, generate meaningful study tasks that help students LEARN and PRACTICE the concepts.
Each task should directly relate to the lesson content and require active learning or coding.

=== CRITICAL RULES - READ CAREFULLY ===
 IMPORTANT: Even if lesson titles mention ""installation"" or ""setup"", DO NOT create tasks about installing software!
Instead, focus on USING the technology after it's already installed.

1. ONLY generate tasks about LEARNING CONTENT from the lessons:
   - Practice tasks: Write code, solve problems, build features, implement algorithms
   - Theory tasks: Understand concepts, explain principles, analyze patterns
   - Quiz tasks: Test knowledge with specific questions about lesson content

2. ABSOLUTELY FORBIDDEN - DO NOT generate tasks about:
    Installing software (""Install Docker"", ""Install Python"", ""Setup IDE"", ""Download tools"")
   Verifying installation (""Verify Docker installation"", ""Check version"")
   Environment setup (""Configure environment"", ""Setup workspace"")
   Downloading files or resources
   System configuration or prerequisites
   Administrative or preparation tasks

3. CORRECT APPROACH - If lessons mention installation:
   Instead of: ""Install Docker"" → Create: ""Build and run a Docker container""
   Instead of: ""Setup Python environment"" → Create: ""Write a Python script using virtual environments""
   Instead of: ""Verify installation"" → Create: ""Use Docker commands to manage containers""

4. For Practice tasks (TaskType: ""Practice"", VerificationMethod: ""CodeSubmission""):
   - Must require writing actual code
   - Must be specific programming exercises based on lesson content
   - VerificationPrompt should describe what to check in the submitted code
   - Examples: 
     * ""Build a Docker container for a web application""
     * ""Create a Dockerfile with multi-stage builds""
     * ""Implement a sorting algorithm in Python""
     * ""Build a REST API endpoint with authentication""

5. For Theory tasks (TaskType: ""Theory""):
   - Use VerificationMethod: ""QuickQuiz"" with 2-3 questions
   - Questions must test understanding of specific concepts from lessons
   - Each question needs 4 options with correctAnswer index (0-3)
   - OR use VerificationMethod: ""SummarySubmission"" with VerificationPrompt describing what to summarize

6. Task Quality Requirements:
   - Title: Specific and actionable (not vague like ""Learn basics"")
   - Description: Clear instructions on what to do
   - Priority: High (core concepts), Medium (important), Low (optional practice)
   - Generate 2-4 tasks depending on lesson complexity
   - Write in the same language as the chapter title

=== OUTPUT FORMAT ===
Return ONLY valid JSON (no markdown, no extra text):
{{
  ""tasks"": [
    {{
      ""title"": ""Implement bubble sort algorithm"",
      ""description"": ""Write a function that implements the bubble sort algorithm to sort an array of integers in ascending order."",
      ""priority"": ""High"",
      ""taskType"": ""Practice"",
      ""verificationMethod"": ""CodeSubmission"",
      ""verificationPrompt"": ""Verify the code correctly implements bubble sort with proper comparisons and swaps. Check for correct time complexity understanding."",
      ""minimumScore"": 70,
      ""quizQuestions"": null
    }},
    {{
      ""title"": ""Understanding sorting algorithms complexity"",
      ""description"": ""Test your knowledge about time and space complexity of different sorting algorithms."",
      ""priority"": ""Medium"",
      ""taskType"": ""Theory"",
      ""verificationMethod"": ""QuickQuiz"",
      ""verificationPrompt"": null,
      ""minimumScore"": 70,
      ""quizQuestions"": [
        {{
          ""question"": ""What is the average time complexity of bubble sort?"",
          ""options"": [""O(n)"", ""O(n log n)"", ""O(n²)"", ""O(log n)""],
          ""correctAnswer"": 2
        }},
        {{
          ""question"": ""Which sorting algorithm has O(n log n) average complexity?"",
          ""options"": [""Bubble sort"", ""Selection sort"", ""Merge sort"", ""Insertion sort""],
          ""correctAnswer"": 2
        }}
      ]
    }}
  ]
}}";
    }
}
