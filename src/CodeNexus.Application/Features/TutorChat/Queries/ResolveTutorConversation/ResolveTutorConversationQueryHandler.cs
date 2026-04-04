using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.TutorChat.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.TutorChat.Queries.ResolveTutorConversation;

public class ResolveTutorConversationQueryHandler
    : IRequestHandler<ResolveTutorConversationQuery, Result<ResolveTutorConversationResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ResolveTutorConversationQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<ResolveTutorConversationResponseDto>> Handle(ResolveTutorConversationQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();
        if (userId == Guid.Empty)
        {
            return Result<ResolveTutorConversationResponseDto>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        if (!request.LearningPathId.HasValue && !request.ChapterId.HasValue && !request.LessonId.HasValue)
        {
            return Result<ResolveTutorConversationResponseDto>.Failure("CONTEXT_REQUIRED", "Learning path, chapter, or lesson id is required");
        }

        var context = await LoadContextAsync(userId, request.LearningPathId, request.ChapterId, request.LessonId, cancellationToken);
        if (!context.IsSuccess)
        {
            return Result<ResolveTutorConversationResponseDto>.Failure(context.ErrorCode!, context.ErrorMessage!);
        }

        var existing = await FindConversationByContextAsync(userId, context.Value!, cancellationToken);
        if (existing != null)
        {
            // Upgrade legacy lesson-scoped records to chapter-scoped conversation.
            if (ApplyConversationContext(existing, context.Value!))
            {
                await _context.SaveChangesAsync(cancellationToken);
            }

            return Result<ResolveTutorConversationResponseDto>.Success(
                new ResolveTutorConversationResponseDto(existing.ConversationId, false));
        }

        if (!request.CreateIfMissing)
        {
            return Result<ResolveTutorConversationResponseDto>.Failure("CONVERSATION_NOT_FOUND", "Conversation not found.");
        }

        var config = await _context.AIProviderConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UsageType == AIUsageType.Assistant && c.IsActive, cancellationToken);

        if (config == null)
        {
            return Result<ResolveTutorConversationResponseDto>.Failure("AI_CONFIG_NOT_FOUND", "AI assistant config not found");
        }

        var title = BuildConversationTitle(context.Value!);

        var conversation = new Conversation
        {
            ConversationId = NewId.NextGuid(),
            UserId = userId,
            ConfigId = config.ConfigId,
            Title = title,
            CreatedAt = DateTime.UtcNow,
            MessageCount = 0,
            IsDeleted = false,
            LearningPathId = context.Value!.LearningPathId,
            ChapterId = context.Value!.ChapterId,
            LessonId = context.Value!.ChapterId.HasValue ? null : context.Value!.LessonId
        };

        await _context.Conversations.AddAsync(conversation, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<ResolveTutorConversationResponseDto>.Success(
            new ResolveTutorConversationResponseDto(conversation.ConversationId, true));
    }

    private async Task<Result<TutorContext>> LoadContextAsync(
        Guid userId,
        Guid? learningPathId,
        Guid? chapterId,
        Guid? lessonId,
        CancellationToken cancellationToken)
    {
        LearningPath? learningPath = null;
        Chapter? chapter = null;
        Lesson? lesson = null;

        if (chapterId.HasValue)
        {
            chapter = await _context.Chapters
                .Include(c => c.LearningPath)
                .FirstOrDefaultAsync(c => c.ChapterId == chapterId, cancellationToken);

            if (chapter == null || chapter.IsDeleted)
                return Result<TutorContext>.Failure("CHAPTER_NOT_FOUND", "Chapter not found");

            learningPath = chapter.LearningPath;
        }

        if (lessonId.HasValue)
        {
            lesson = await _context.Lessons
                .Include(l => l.Chapter)
                    .ThenInclude(c => c.LearningPath)
                .FirstOrDefaultAsync(l => l.LessonId == lessonId, cancellationToken);

            if (lesson == null || lesson.IsDeleted)
                return Result<TutorContext>.Failure("LESSON_NOT_FOUND", "Lesson not found");

            if (chapterId.HasValue && lesson.ChapterId != chapterId.Value)
                return Result<TutorContext>.Failure("LESSON_NOT_IN_CHAPTER", "Lesson does not belong to the selected chapter.");

            chapter ??= lesson.Chapter;
            learningPath ??= lesson.Chapter.LearningPath;
        }

        if (learningPathId.HasValue && learningPath == null)
        {
            learningPath = await _context.LearningPaths
                .FirstOrDefaultAsync(lp => lp.PathId == learningPathId, cancellationToken);

            if (learningPath == null)
                return Result<TutorContext>.Failure("LEARNING_PATH_NOT_FOUND", "Learning path not found.");
        }

        if (learningPath != null && learningPath.UserId != userId)
        {
            return Result<TutorContext>.Failure("ACCESS_DENIED", "Access denied.");
        }

        return Result<TutorContext>.Success(new TutorContext(
            learningPath?.PathId,
            chapter?.ChapterId,
            lesson?.LessonId,
            learningPath?.Title,
            chapter?.Title,
            lesson?.Title));
    }

    private static string BuildConversationTitle(TutorContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.ChapterTitle))
            return $"Tutor - {context.ChapterTitle}";
        if (!string.IsNullOrWhiteSpace(context.LearningPathTitle))
            return $"Tutor - {context.LearningPathTitle}";
        if (!string.IsNullOrWhiteSpace(context.LessonTitle))
            return $"Tutor - {context.LessonTitle}";

        return "Tutor Conversation";
    }

    private static bool ApplyConversationContext(Conversation conversation, TutorContext context)
    {
        var changed = false;

        if (context.ChapterId.HasValue)
        {
            if (conversation.LearningPathId != context.LearningPathId)
            {
                conversation.LearningPathId = context.LearningPathId;
                changed = true;
            }

            if (conversation.ChapterId != context.ChapterId)
            {
                conversation.ChapterId = context.ChapterId;
                changed = true;
            }

            if (conversation.LessonId != null)
            {
                conversation.LessonId = null;
                changed = true;
            }

            return changed;
        }

        if (context.LearningPathId.HasValue && !conversation.LearningPathId.HasValue)
        {
            conversation.LearningPathId = context.LearningPathId;
            changed = true;
        }

        return changed;
    }

    private async Task<Conversation?> FindConversationByContextAsync(
        Guid userId,
        TutorContext context,
        CancellationToken cancellationToken)
    {
        if (context.ChapterId.HasValue)
        {
            var chapterConversation = await _context.Conversations
                .Where(c => c.UserId == userId && c.ChapterId == context.ChapterId && !c.IsDeleted)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (chapterConversation != null)
            {
                return chapterConversation;
            }

            if (context.LessonId.HasValue)
            {
                return await _context.Conversations
                    .Where(c => c.UserId == userId && c.LessonId == context.LessonId && !c.IsDeleted)
                    .OrderByDescending(c => c.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);
            }
        }

        if (context.LearningPathId.HasValue)
        {
            return await _context.Conversations
                .Where(c => c.UserId == userId
                            && c.LearningPathId == context.LearningPathId
                            && c.ChapterId == null
                            && !c.IsDeleted)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (context.LessonId.HasValue)
        {
            return await _context.Conversations
                .Where(c => c.UserId == userId && c.LessonId == context.LessonId && !c.IsDeleted)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return null;
    }

    private sealed record TutorContext(
        Guid? LearningPathId,
        Guid? ChapterId,
        Guid? LessonId,
        string? LearningPathTitle,
        string? ChapterTitle,
        string? LessonTitle);
}
