using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Chapters.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
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
            var content = await _aiGeneratorService.GenerateContentAsync(prompt, AIUsageType.ContentGeneration);

            chapter.Content = content;
            chapter.UpdatedAt = DateTime.UtcNow;

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

        var lessonTitles = chapter.Lessons
            .OrderBy(l => l.OrderIndex)
            .Select(l => l.Title);
        var lessons = string.Join(", ", lessonTitles);

        return $@"Given a programming chapter titled ""{chapter.Title}"" in a {subject} learning path (""{learningPath.Title}""), which contains the following lessons: {lessons}.

Write a single short sentence (max 20 words) that describes the main goal/outcome of this chapter. 
The sentence should summarize what the learner will be able to do after completing this chapter.
Write in the same language as the chapter title.
Return ONLY the sentence, no quotes, no markdown, no extra text.";
    }
}
