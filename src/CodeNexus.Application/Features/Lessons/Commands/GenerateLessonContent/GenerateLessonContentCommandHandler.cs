﻿using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Lessons.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Lessons.Commands.GenerateLessonContent;

public class GenerateLessonContentCommandHandler : IRequestHandler<GenerateLessonContentCommand, Result<LessonContentDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAIGeneratorService _aiGeneratorService;

    public GenerateLessonContentCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IAIGeneratorService aiGeneratorService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _aiGeneratorService = aiGeneratorService;
    }

    public async Task<Result<LessonContentDto>> Handle(GenerateLessonContentCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var lesson = await _context.Lessons
                .Include(l => l.Chapter)
                .ThenInclude(c => c.LearningPath)
                    .ThenInclude(lp => lp.Subject)

                .Include(l => l.Chapter)
                .ThenInclude(c => c.LearningPath)
                    .ThenInclude(lp => lp.Chapters)
                        .ThenInclude(c => c.Lessons)


            .FirstOrDefaultAsync(l => l.LessonId == request.LessonId, cancellationToken);
        if (lesson == null)
            return Result<LessonContentDto>.Failure("LESSON_NOT_FOUND", "Lesson not found");

        if (lesson.Chapter.LearningPath.UserId != userId)
            return Result<LessonContentDto>.Failure("UNAUTHORIZED", "User not authenticated");

        if (lesson.UpdatedAt != null)
        {
            return Result<LessonContentDto>.Success(
                new LessonContentDto(lesson.LessonId, lesson.Title, lesson.Content));
        }

        try
        {
            var language = lesson.Chapter.LearningPath.Language;
            var prompt = BuildPrompt(lesson, lesson.Chapter, lesson.Chapter.LearningPath, language);
            var content = await _aiGeneratorService.GenerateContentAsync(prompt, AIUsageType.ContentGeneration);

            lesson.Content = content;
            lesson.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            return Result<LessonContentDto>.Success(
                new LessonContentDto(lesson.LessonId, lesson.Title, content));
        }
        catch (Exception ex)
        {
            return Result<LessonContentDto>.Failure("CONTENT_GENERATION_FAILED",
                "Failed to generate content.");
        }
    }

    private static string BuildPrompt(Lesson lesson, Chapter chapter, LearningPath learningPath, LanguageSelection language)
    {
        var subject = learningPath.Subject.Name;
        var lang = subject.ToLowerInvariant();

        var languageInstruction = language switch
        {
            LanguageSelection.VietNamese => @"
=== LANGUAGE REQUIREMENTS ===
- Generate ALL content in Vietnamese language
- IMPORTANT: Keep technical terms in English when translating to Vietnamese would cause confusion or change meaning
- Examples of terms to keep in English: API, REST, JSON, Docker, Kubernetes, Framework, Library, Algorithm, etc.
- Use Vietnamese for general descriptions and explanations
- Example: ""Giới thiệu về REST API"" (correct) instead of ""Giới thiệu về API nghỉ ngơi"" (wrong)
",
            LanguageSelection.English => @"
=== LANGUAGE REQUIREMENTS ===
- Generate ALL content in English language
- Use clear, professional English
",
            _ => ""
        };

        var outlineLines = new List<string>();
        foreach (var ch in learningPath.Chapters.OrderBy(c => c.OrderIndex))
        {
            var chMarker = ch.ChapterId == chapter.ChapterId ? " ? current chapter" : "";
            outlineLines.Add($"Chapter {ch.OrderIndex + 1}: {ch.Title}{chMarker}");

            foreach (var ls in ch.Lessons.OrderBy(l => l.OrderIndex))
            {
                var lsMarker = ls.LessonId == lesson.LessonId ? " ? GENERATE THIS" : "";
                outlineLines.Add($"  {ls.OrderIndex + 1}. {ls.Title}{lsMarker}");
            }
        }

        var outline = string.Join("\n", outlineLines);

        return $@"You are a senior {subject} developer and programming instructor.
Write a focused, easy-to-follow programming lesson in Markdown.

=== CONTEXT ===
Language: {subject}
Learning Path: {learningPath.Title}
Full outline:
{outline}
Current lesson brief: {lesson.Content}

{languageInstruction}

=== STRUCTURE ===
1. Overview (2-3 sentences)
2. Core Concepts (bullet points, concise explanations)
3. Code Examples
   * Use ```{lang} for all code blocks
   * Inline comments on key lines
   * Simple → complex, real-world scenarios only
4. Common Mistakes
   * Use strict Markdown format for easier parsing
   * Include 1-5 mistakes
   * For each mistake, follow this exact template:
     #### Mistake <number>: <short name>
     **Wrong**
     ```{lang}
     // wrong code
     ```
     **Correct**
     ```{lang}
     // correct code
     ```
   * Keep code snippets short (3-8 lines each)
5. Best Practices (3-5 short tips)
6. Summary (bullet point takeaways)

=== OUTPUT CONTRACT (STRICT) ===
Return content in this exact section order and exact headings:
## Overview
## Core Concepts
## Code Examples
## Common Mistakes
## Best Practices
## Summary

For easier frontend parsing, wrap each section with markers:
<!-- SECTION:overview:start --> ... <!-- SECTION:overview:end -->
<!-- SECTION:core-concepts:start --> ... <!-- SECTION:core-concepts:end -->
<!-- SECTION:code-examples:start --> ... <!-- SECTION:code-examples:end -->
<!-- SECTION:common-mistakes:start --> ... <!-- SECTION:common-mistakes:end -->
<!-- SECTION:best-practices:start --> ... <!-- SECTION:best-practices:end -->
<!-- SECTION:summary:start --> ... <!-- SECTION:summary:end -->

Inside Common Mistakes, every item MUST use exactly this format:
#### Mistake <number>: <short name>
**Wrong**
```{lang}
// wrong code
```
**Correct**
```{lang}
// correct code
```

If this is a review/recap lesson, summarize and connect key concepts from all previous lessons shown in the outline above.

=== RULES ===
* ## for sections, ### for subsections
* Do NOT start with the lesson title as heading
* All code must be valid, runnable {subject}
* Explain code step by step, not just show it
* No foo/bar — use practical examples
* Keep content concise and focused — easy to absorb for self-learners
* Use the exact headings, order, and section markers from OUTPUT CONTRACT
* In Common Mistakes, follow the exact Markdown template above for every mistake
Markdown only.";
    }
}
