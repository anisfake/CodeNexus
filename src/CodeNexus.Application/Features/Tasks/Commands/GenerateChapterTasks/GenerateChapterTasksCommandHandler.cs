using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Helpers;
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
                .Include(c => c.LearningPath)
                    .ThenInclude(lp => lp.Chapters)
                .Include(c => c.Lessons)
                .Include(c => c.Tasks)
            .FirstOrDefaultAsync(c => c.ChapterId == request.ChapterId, cancellationToken);

        if (chapter == null)
            return Result<ChapterTasksDto>.Failure("CHAPTER_NOT_FOUND", "Chapter not found");

        if (chapter.LearningPath.UserId != userId)
            return Result<ChapterTasksDto>.Failure("UNAUTHORIZED", "User not authenticated");

        if (!chapter.Lessons.Any())
            return Result<ChapterTasksDto>.Failure("CHAPTER_NO_LESSONS", "Chapter has no lessons to generate tasks from");

        if (chapter.Tasks.Any())
        {
            return Result<ChapterTasksDto>.Success(MapToDto(chapter));
        }

        try
        {
            var language = chapter.LearningPath.Language;
            var taskCount = CalculateTaskCount(chapter.Lessons.Count);
            var prompt = BuildPrompt(chapter, language, taskCount);
            var generated = await _aiGeneratorService.GenerateStructureAsync<GeneratedTasksDto>(prompt, AIUsageType.ContentGeneration);

            if (generated?.Tasks == null || generated.Tasks.Count == 0)
                return Result<ChapterTasksDto>.Failure("INVALID_AI_RESPONSE", "AI returned invalid response.");

            var validTasks = generated.Tasks
                .Where(t => !IsInvalidTask(t.Title, t.Description))
                .ToList();

            if (validTasks.Count == 0)
                return Result<ChapterTasksDto>.Failure("NO_VALID_TASKS", "AI generated only invalid tasks (setup/installation). Please regenerate.");

            var dueDates = CalculateTaskDueDates(chapter, validTasks.Count);

            for (int i = 0; i < validTasks.Count; i++)
            {
                var t = validTasks[i];
                var task = new Domain.Entities.Tasks
                {
                    TaskId = NewId.NextGuid(),
                    ChapterId = chapter.ChapterId,
                    PathId = chapter.PathId,
                    Title = t.Title,
                    Description = t.Description,
                    DueDate = dueDates[i],
                    Priority = ParsePriority(t.Priority),
                    Status = TaskStatus_.Pending,
                    CreatedAt = DateTime.UtcNow,
                    TaskType = ParseTaskType(t.TaskType),
                    VerificationPrompt = t.VerificationPrompt,
                    MinimumScore = t.MinimumScore ?? 70,
                    QuizQuestionsJson = ParseTaskType(t.TaskType) == TaskType.Quizz && t.QuizQuestions != null && t.QuizQuestions.Any()
                        ? System.Text.Json.JsonSerializer.Serialize(t.QuizQuestions)
                        : null
                };

                await _context.Tasks.AddAsync(task, cancellationToken);
            }

            await _context.SaveChangesAsync(cancellationToken);

            var hasGoalItemMappingChanges = await LearningPathGoalSemanticMappingHelper.RebuildForPathAsync(
                _context,
                _aiGeneratorService,
                chapter.PathId,
                chapter.LearningPath.Language,
                cancellationToken);
            if (hasGoalItemMappingChanges)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }

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
                t.Description ?? string.Empty,
                t.DueDate,
                t.TaskType,
                t.Priority,
                t.Status,
                t.QuizQuestionsJson
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

    private static int CalculateTaskCount(int lessonCount)
    {
        return lessonCount switch
        {
            <= 2 => 2,      // 1-2 lessons → 2 tasks
            3 => 3,         // 3 lessons → 3 tasks
            4 => 3,         // 4 lessons → 3 tasks
            5 => 4,         // 5 lessons → 4 tasks
            6 => 4,         // 6 lessons → 4 tasks
            7 => 5,         // 7 lessons → 5 tasks
            _ => 6          // 8+ lessons → 6 tasks (max)
        };
    }

    private static List<DateTime?> CalculateTaskDueDates(Domain.Entities.Chapter chapter, int taskCount)
    {
        var learningPath = chapter.LearningPath;
        var dueDates = new List<DateTime?>();

        if (learningPath.EndDate == null)
        {
            for (int i = 0; i < taskCount; i++)
                dueDates.Add(null);
            return dueDates;
        }

        var allChapters = learningPath.Chapters.OrderBy(c => c.OrderIndex).ToList();
        var currentChapterIndex = allChapters.FindIndex(c => c.ChapterId == chapter.ChapterId);

        if (currentChapterIndex == -1)
        {
            for (int i = 0; i < taskCount; i++)
                dueDates.Add(null);
            return dueDates;
        }

        var totalChapters = allChapters.Count;
        var totalDays = (learningPath.EndDate.Value - learningPath.StartDate.Value).TotalDays;
        var daysPerChapter = totalDays / totalChapters;

        var chapterStartDate = learningPath.StartDate?.AddDays(currentChapterIndex * daysPerChapter);
        var chapterEndDate = learningPath.StartDate?.AddDays((currentChapterIndex + 1) * daysPerChapter);

        var daysPerTask = (chapterEndDate.Value - chapterStartDate.Value).TotalDays / taskCount;

        for (int i = 0; i < taskCount; i++)
        {
            var dueDate = chapterStartDate.Value.AddDays((i + 1) * daysPerTask);
            dueDates.Add(dueDate);
        }

        return dueDates;
    }

    private static string BuildPrompt(Domain.Entities.Chapter chapter, LanguageSelection language, int taskCount)
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
- CRITICAL: ALWAYS keep ALL technical terms, programming concepts, and technology names in ENGLISH
- DO NOT translate technical terms to Vietnamese under any circumstances
- Examples of terms that MUST stay in English:
  * Data structures: Array, Stack, Queue, Tree, Graph, Heap, Hash Table, Linked List
  * Algorithms: Bubble Sort, Quick Sort, Merge Sort, Binary Search, DFS, BFS, Dynamic Programming
  * Programming: API, REST, JSON, XML, Framework, Library, Interface, Class, Function, Variable
  * Technologies: Docker, Kubernetes, React, Angular, Node.js, MongoDB, SQL, NoSQL
  * Concepts: Recursion, Iteration, Polymorphism, Inheritance, Encapsulation, Abstraction
- Use Vietnamese ONLY for:
  * General descriptions and explanations
  * Action words (viết, tạo, thực hiện, xây dựng, etc.)
  * Connecting phrases and sentences
- Correct examples:
  * ""Viết hàm Bubble Sort"" ✓
  * ""Thực hành với Stack và Queue"" ✓
  * ""Xây dựng REST API"" ✓
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
   - Installing software (""Install .NET SDK"", ""Setup IDE"", ""Download tools"")
   - Verifying installation (""Check .NET version"", ""Verify setup"")
   - Environment setup (""Configure environment"", ""Setup workspace"")
   - Running/Testing applications (""Run app in browser"", ""Test the application"", ""Start the server"")
   - Downloading files or resources
   - System configuration or prerequisites
   - Administrative or preparation tasks

3. CORRECT APPROACH - Focus on CODE WRITING:
   Instead of: ""Create and run ASP.NET app"" → Create: ""Write a Controller with action methods""
   Instead of: ""Test the application"" → Create: ""Implement error handling in Controller""
   Instead of: ""Run in browser"" → Create: ""Create View with Razor syntax""

4. For Practice tasks (TaskType: ""Practice""):
   - All practice tasks require students to WRITE CODE ONLY
   - Focus on specific code components, not running/testing
   - VerificationPrompt: describe what to check in the submitted code
   - Examples: 
     * ""Write a Controller class with GET and POST actions"" → student submits Controller code
     * ""Implement Model class with data annotations"" → student submits Model code
     * ""Create Razor View with form elements"" → student submits View code
     * ""Write Middleware class for request logging"" → student submits Middleware code

5. For Theory tasks (TaskType: ""Theory""):
   - VerificationPrompt: describe what to summarize

6. For Quiz tasks (TaskType: ""Quizz""):
   - Include 3-5 questions in quizQuestions array
   - Questions should test comprehensive understanding of lesson concepts
   - Each question needs 4 options with correctAnswer index (0-3)
   - Focus on practical application and deeper understanding

7. Task Quality Requirements:
   - Title: Specific and actionable (not vague like ""Learn basics"")
   - Description: Clear instructions on what to do
   - Priority: High (core concepts), Medium (important), Low (optional practice)
   - Generate EXACTLY {taskCount} tasks (no more, no less)
   - Distribute tasks evenly across lessons (don't focus on just one lesson)
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
      ""verificationPrompt"": ""Verify the code correctly implements bubble sort with proper comparisons and swaps. Check for correct time complexity understanding."",
      ""minimumScore"": 70,
      ""quizQuestions"": null
    }},
    {{
      ""title"": ""Build a simple calculator function"",
      ""description"": ""Create a calculator function that can perform basic arithmetic operations (add, subtract, multiply, divide)."",
      ""priority"": ""Medium"",
      ""taskType"": ""Practice"",
      ""verificationPrompt"": ""Check if the function handles all four operations correctly, includes error handling for division by zero, and has proper input validation."",
      ""minimumScore"": 70,
      ""quizQuestions"": null
    }},
    {{
      ""title"": ""Understanding sorting algorithms complexity"",
      ""description"": ""Write a summary explaining the time and space complexity of different sorting algorithms including bubble sort, merge sort, and quick sort."",
      ""priority"": ""Medium"",
      ""taskType"": ""Theory"",
      ""verificationPrompt"": ""Check if the summary covers time complexity, space complexity, and practical use cases for each algorithm mentioned."",
      ""minimumScore"": 70,
      ""quizQuestions"": null
    }},
    {{
      ""title"": ""Comprehensive algorithm knowledge test"",
      ""description"": ""Complete quiz covering all algorithm concepts from this chapter including implementation details and performance analysis."",
      ""priority"": ""High"",
      ""taskType"": ""Quizz"",
      ""verificationPrompt"": null,
      ""minimumScore"": 80,
      ""quizQuestions"": [
        {{
          ""question"": ""What is the space complexity of merge sort?"",
          ""options"": [""O(1)"", ""O(log n)"", ""O(n)"", ""O(n²)""],
          ""correctAnswer"": 2
        }},
        {{
          ""question"": ""Which algorithm is most suitable for nearly sorted arrays?"",
          ""options"": [""Quick sort"", ""Merge sort"", ""Insertion sort"", ""Heap sort""],
          ""correctAnswer"": 2
        }},
        {{
          ""question"": ""What is the worst-case time complexity of quick sort?"",
          ""options"": [""O(n log n)"", ""O(n²)"", ""O(n)"", ""O(log n)""],
          ""correctAnswer"": 1
        }}
      ]
    }}
  ]
}}";
    }
}
