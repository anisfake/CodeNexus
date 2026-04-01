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
            return Result<ChapterContentDto>.Failure("UNAUTHORIZED", "User not authenticated");

        if (chapter.UpdatedAt != null)
        {
            return Result<ChapterContentDto>.Success(
                new ChapterContentDto(chapter.ChapterId, chapter.Title, chapter.Content!));
        }

        try
        {
            var language = chapter.LearningPath.Language;
            var prompt = BuildPrompt(chapter, chapter.LearningPath, language);
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
                "Failed to generate content.");
        }
    }

    private static string BuildPrompt(Chapter chapter, LearningPath learningPath, LanguageSelection language)
    {
        var subject = learningPath.Subject.Name;

        var lessonTitles = chapter.Lessons
            .OrderBy(l => l.OrderIndex)
            .Select(l => l.Title);
        var lessons = string.Join(", ", lessonTitles);

        var languageInstruction = language switch
        {
            LanguageSelection.VietNamese => "Write the sentence in Vietnamese. Keep technical terms in English.",
            LanguageSelection.English => "Write the sentence in English.",
            _ => ""
        };

        return $@"Given a chapter titled ""{chapter.Title}"" in the learning path ""{learningPath.Title}"" for the subject {subject}, which contains the following lessons: {lessons}.

Write a single short sentence describing what this chapter helps the learner achieve.
The sentence should summarize what the learner will be able to do after completing this chapter.
Keep it concise, natural, and aligned with the chapter summary format used in the learning path skeleton.
{languageInstruction}
Return ONLY the sentence, no quotes, no markdown, no extra text.";
    }
}
