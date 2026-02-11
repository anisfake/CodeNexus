using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Chapters.DTOs;
using CodeNexus.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Chapters.Commands.GenerateChapterContent;

public class GenerateChapterContentCommandHandler : IRequestHandler<GenerateChapterContentCommand, Result<ChapterContentDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAIGeneratorService _aiGeneratorService;

    public GenerateChapterContentCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IAIGeneratorService aiGeneratorService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _aiGeneratorService = aiGeneratorService;
    }

    public async Task<Result<ChapterContentDto>> Handle(GenerateChapterContentCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var chapter = await _context.Chapters
                .Include(c => c.LearningPath)
                    .ThenInclude(lp => lp.Subject)
                .Include(c => c.LearningPath)
                    .ThenInclude(lp => lp.Chapters)
                    .ThenInclude(ch => ch.Lessons)
                .Include(c => c.Lessons)
            .FirstOrDefaultAsync(c => c.ChapterId == request.ChapterId, cancellationToken);

        if (chapter == null)
            return Result<ChapterContentDto>.Failure("CHAPTER_NOT_FOUND", "Chapter not found");

        if (chapter.LearningPath.UserId != userId)
            return Result<ChapterContentDto>.Failure("UNAUTHORIZED", "You do not have access to this chapter");

        if (chapter.UpdatedAt != null)
        {
            return Result<ChapterContentDto>.Success(
                new ChapterContentDto(chapter.ChapterId, chapter.Title, chapter.Content!));
        }

        try
        {
            var prompt = BuildPrompt(chapter, chapter.LearningPath);
            var content = await _aiGeneratorService.GenerateContentAsync(prompt);

            chapter.Content = content;
            chapter.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync(cancellationToken);

            return Result<ChapterContentDto>.Success(
                new ChapterContentDto(chapter.ChapterId, chapter.Title, content));
        }
        catch (Exception ex)
        {
            return Result<ChapterContentDto>.Failure("CONTENT_GENERATION_FAILED",
                $"Failed to generate chapter content: {ex.Message}");
        }
    }

    private static string BuildPrompt(Chapter chapter, LearningPath learningPath)
    {
        var subject = learningPath.Subject.Name;

        var outlineLines = new List<string>();
        foreach (var ch in learningPath.Chapters.OrderBy(c => c.OrderIndex))
        {
            var chMarker = ch.ChapterId == chapter.ChapterId ? " ? current chapter" : "";
            outlineLines.Add($"Chapter {ch.OrderIndex + 1}: {ch.Title}{chMarker}");

            foreach (var ls in ch.Lessons.OrderBy(l => l.OrderIndex))
            {
                outlineLines.Add($"  {ls.OrderIndex + 1}. {ls.Title}");
            }
        }

        var outline = string.Join("\n", outlineLines);

        return $@"You are a senior {subject} developer and programming instructor.
Write a concise chapter description/overview in Markdown.

=== CONTEXT ===
Language: {subject}
Learning Path: {learningPath.Title}

Full outline:
{outline}

Current chapter brief: {chapter.Content}

=== STRUCTURE ===
1. ## Chapter Overview (3-5 sentences summarizing what this chapter covers and why it matters)
2. ## What You Will Learn (bullet list of key learning objectives)
3. ## Prerequisites (what the learner should know before starting, or ""None"" if first chapter)
4. ## Lessons in This Chapter (brief 1-sentence description for each lesson listed above)

=== RULES ===
- ## for sections, ### for subsections
- Do NOT start with the chapter title as heading
- Keep it concise and motivating for self-learners
- Focus on the ""why"" — explain why these topics matter in real-world {subject} development
- Write in the same language as the chapter title
- This is an overview/description, NOT a full lesson — keep it short and focused

Markdown only.";
    }
}
