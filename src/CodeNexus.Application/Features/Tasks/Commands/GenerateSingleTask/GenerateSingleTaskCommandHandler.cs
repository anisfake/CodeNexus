using System.Text.RegularExpressions;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Helpers;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Tasks.DTOs;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Tasks.Commands.GenerateSingleTask;

public class GenerateSingleTaskCommandHandler : IRequestHandler<GenerateSingleTaskCommand, Result<TaskItemDto>>
{
	private readonly IApplicationDbContext _context;
	private readonly ICurrentUserService _currentUserService;
	private readonly IAIGeneratorService _aiGeneratorService;

	public GenerateSingleTaskCommandHandler(
		IApplicationDbContext context,
		ICurrentUserService currentUserService,
		IAIGeneratorService aiGeneratorService)
	{
		_context = context;
		_currentUserService = currentUserService;
		_aiGeneratorService = aiGeneratorService;
	}

	public async Task<Result<TaskItemDto>> Handle(GenerateSingleTaskCommand request, CancellationToken cancellationToken)
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
			return Result<TaskItemDto>.Failure("CHAPTER_NOT_FOUND", "Chapter not found");

		if (chapter.LearningPath.UserId != userId)
			return Result<TaskItemDto>.Failure("UNAUTHORIZED", "User not authenticated");

		if (!chapter.Lessons.Any())
			return Result<TaskItemDto>.Failure("CHAPTER_NO_LESSONS", "Chapter has no lessons to generate tasks from");

		if (string.IsNullOrWhiteSpace(chapter.Title))
			return Result<TaskItemDto>.Failure("CHAPTER_TITLE_REQUIRED", "Chapter title is required to generate task");

		var hasAnyLessonTitle = chapter.Lessons.Any(l => !string.IsNullOrWhiteSpace(l.Title));
		if (!hasAnyLessonTitle)
			return Result<TaskItemDto>.Failure("LESSON_TITLE_REQUIRED", "At least one lesson title is required to generate task");

		if (request.TaskType is not TaskType.Practice and not TaskType.Theory)
			return Result<TaskItemDto>.Failure("TASK_TYPE_NOT_SUPPORTED", "Only Practice and Theory task types are supported for AI generation.");

		try
		{
			var prompt = BuildPrompt(chapter, request.Title, request.TaskType);
			var generated = await _aiGeneratorService.GenerateStructureAsync<GeneratedTasksDto>(prompt, AIUsageType.ContentGeneration);

			if (generated?.Tasks == null || generated.Tasks.Count == 0)
				return Result<TaskItemDto>.Failure("INVALID_AI_RESPONSE", "AI returned invalid response.");

			var generatedTask = generated.Tasks.FirstOrDefault(t => !IsInvalidTask(t.Title));

			if (generatedTask == null)
			{
				var strictPrompt = prompt + "\n=== STRICT FILTER ===\nDO NOT include ANY task related to install/setup/download/configure. Return ONLY coding/theory tasks.";
				generated = await _aiGeneratorService.GenerateStructureAsync<GeneratedTasksDto>(strictPrompt, AIUsageType.ContentGeneration);
				generatedTask = generated?.Tasks?.FirstOrDefault(t => !IsInvalidTask(t.Title));

				if (generatedTask == null)
					return Result<TaskItemDto>.Failure("NO_VALID_TASKS", "AI could not generate a valid task. Please adjust chapter context or try again.");
			}

			var finalTitle = string.IsNullOrWhiteSpace(request.Title)
				? generatedTask.Title?.Trim()
				: request.Title.Trim();

			if (string.IsNullOrWhiteSpace(finalTitle))
				return Result<TaskItemDto>.Failure("INVALID_AI_RESPONSE", "Task title cannot be empty.");

			var finalDescription = generatedTask.Description?.Trim() ?? string.Empty;

			var existingActiveTasks = chapter.Tasks
				.Where(t => !t.IsDeleted)
				.ToList();

			if (IsDuplicateTask(existingActiveTasks, request.TaskType, finalTitle, finalDescription))
				return Result<TaskItemDto>.Failure("DUPLICATE_TASK", "Generated task is too similar to an existing task in this chapter.");

			var task = new Domain.Entities.Tasks
			{
				TaskId = NewId.NextGuid(),
				ChapterId = chapter.ChapterId,
				PathId = chapter.PathId,
				Title = finalTitle,
				Description = finalDescription,
				DueDate = CalculateDueDate(chapter),
				Priority = ParsePriority(generatedTask.Priority),
				Status = TaskStatus_.Pending,
				CreatedAt = DateTime.UtcNow,
				TaskType = request.TaskType,
				VerificationPrompt = generatedTask.VerificationPrompt,
				MinimumScore = generatedTask.MinimumScore ?? 70,
				QuizQuestionsJson = null
			};

			await _context.Tasks.AddAsync(task, cancellationToken);
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

			var dto = new TaskItemDto(
				task.TaskId,
				task.Title,
				task.Description ?? string.Empty,
				task.DueDate,
				task.TaskType,
				task.Priority,
				task.Status,
				task.QuizQuestionsJson
			);

			return Result<TaskItemDto>.Success(dto);
		}
		catch (Exception ex)
		{
			return Result<TaskItemDto>.Failure("TASK_GENERATION_FAILED", $"Failed to generate single task: {ex.Message}");
		}
	}

	private static bool IsInvalidTask(string title)
	{
		if (string.IsNullOrWhiteSpace(title))
			return true;

		var titleLower = title.Trim().ToLowerInvariant();

		var invalidKeywords = new[]
		{
			"install", "cài đặt", "download", "tải xuống", "setup", "thiết lập",
			"configure environment", "cấu hình môi trường", "verify installation",
			"kiểm tra cài đặt", "check version", "kiểm tra phiên bản"
		};


		return invalidKeywords.Any(keyword =>
			Regex.IsMatch(titleLower, $@"\b{Regex.Escape(keyword)}\b", RegexOptions.IgnoreCase));
	}

	private static bool IsDuplicateTask(List<Domain.Entities.Tasks> existingTasks, TaskType taskType, string title, string description)
	{
		var normalizedTitle = NormalizeText(title);
		var normalizedDescription = NormalizeText(description);

		return existingTasks
			.Where(t => t.TaskType == taskType)
			.Any(t =>
			NormalizeText(t.Title) == normalizedTitle ||
			(NormalizeText(t.Title) == normalizedTitle && NormalizeText(t.Description) == normalizedDescription));
	}
	private static string NormalizeText(string? text)
	{
		if (string.IsNullOrWhiteSpace(text))
			return string.Empty;

		var filtered = text
			.Trim()
			.ToLowerInvariant()
			.Where(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c))
			.ToArray();

		return string.Join(' ', new string(filtered)
			.Split(' ', StringSplitOptions.RemoveEmptyEntries));
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

	private static DateTime? CalculateDueDate(Domain.Entities.Chapter chapter)
	{
		var learningPath = chapter.LearningPath;

		if (learningPath.StartDate == null || learningPath.EndDate == null)
			return null;

		var allChapters = learningPath.Chapters.OrderBy(c => c.OrderIndex).ToList();
		var currentChapterIndex = allChapters.FindIndex(c => c.ChapterId == chapter.ChapterId);

		if (currentChapterIndex == -1 || allChapters.Count == 0)
			return null;

		var totalDays = (learningPath.EndDate.Value - learningPath.StartDate.Value).TotalDays;
		var daysPerChapter = totalDays / allChapters.Count;

		return learningPath.StartDate.Value.AddDays((currentChapterIndex + 1) * daysPerChapter);
	}

	private static string BuildPrompt(Domain.Entities.Chapter chapter, string? preferredTitle, TaskType taskType)
	{
		var learningPath = chapter.LearningPath;
		var lessonTitles = chapter.Lessons
			.OrderBy(l => l.OrderIndex)
			.Select(l => l.Title);

		var lessons = string.Join("\n- ", lessonTitles);
		var languageInstruction = BuildLanguageInstruction(learningPath.Language);
		var taskTypeLabel = taskType switch
		{
			TaskType.Theory => "Theory",
			_ => "Practice"
		};

		var titleInstruction = string.IsNullOrWhiteSpace(preferredTitle)
			? "- Task title can be generated by you, but it must be specific and actionable."
			: $"- MUST use this exact title for the task: \"{preferredTitle.Trim()}\".";

		var existingTaskTitles = chapter.Tasks
			.Where(t => !t.IsDeleted && t.TaskType == taskType && !string.IsNullOrWhiteSpace(t.Title))
			.Select(t => t.Title.Trim())
			.Distinct()
			.ToList();

		var existingTaskTitlesInstruction = existingTaskTitles.Count == 0
			? $"- No existing {taskTypeLabel} tasks in this chapter."
			: $"- Existing {taskTypeLabel} task titles (MUST avoid duplicates):\n- {string.Join("\n- ", existingTaskTitles)}";

		return $$"""
You are a study planning assistant.

=== CONTEXT ===
Subject: {{learningPath.Subject.Name}}
Learning path title: {{learningPath.Title}}
Learning path description: {{learningPath.Description ?? "N/A"}}
Chapter title: {{chapter.Title}}
Chapter description: {{chapter.Content ?? "N/A"}}
Lessons in this chapter:
- {{lessons}}

{{languageInstruction}}

=== TASK ===
Generate EXACTLY 1 task with TaskType = "{{taskTypeLabel}}" based on chapter and lessons above.

=== REQUIREMENTS ===
{{titleInstruction}}
- FOCUS ONLY on: coding exercises, debugging tasks, conceptual challenges, or algorithmic problems.
- STRICTLY AVOID installation, environment setup, downloads, or configuration steps.
- Description must be concrete and directly related to the lessons.
- Priority must be one of: High, Medium, Low.
- MUST NOT duplicate existing tasks with the same TaskType in this chapter.
- For Practice: include verificationPrompt to check submitted code.
- For Theory: include verificationPrompt to check learner summary.
- taskType MUST remain "{{taskTypeLabel}}" and never be quiz.

=== EXISTING TASKS ===
{{existingTaskTitlesInstruction}}

=== OUTPUT FORMAT ===
Return ONLY valid JSON:
{
  "tasks": [
    {
      "title": "...",
      "description": "...",
      "priority": "High|Medium|Low",
      "taskType": "{{taskTypeLabel}}",
      "verificationPrompt": "...",
      "minimumScore": 70,
      "quizQuestions": null
    }
  ]
}
""";
	}

	private static string BuildLanguageInstruction(LanguageSelection language)
	{
		return language switch
		{
			LanguageSelection.VietNamese => @"=== LANGUAGE REQUIREMENTS ===
- Generate content in Vietnamese.
- Keep technical terms in English.",
			LanguageSelection.English => @"=== LANGUAGE REQUIREMENTS ===
- Generate content in English.",
			_ => string.Empty
		};
	}
}

