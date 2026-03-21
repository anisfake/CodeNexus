using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.TutorChat.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.TutorChat.Commands.SendTutorMessage;

public class SendTutorMessageCommandHandler : IRequestHandler<SendTutorMessageCommand, Result<TutorChatResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAIGeneratorService _aiGeneratorService;
    private readonly IPlanUsageLimitService _planUsageLimitService;

    public SendTutorMessageCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IAIGeneratorService aiGeneratorService,
        IPlanUsageLimitService planUsageLimitService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _aiGeneratorService = aiGeneratorService;
        _planUsageLimitService = planUsageLimitService;
    }

    public async Task<Result<TutorChatResponseDto>> Handle(SendTutorMessageCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();
        if (userId == Guid.Empty)
        {
            return Result<TutorChatResponseDto>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return Result<TutorChatResponseDto>.Failure("EMPTY_MESSAGE", "Message is required");
        }

        var tutorLimitCheck = await _planUsageLimitService.CheckTutorMessageAllowedAsync(userId, cancellationToken);
        if (!tutorLimitCheck.IsSuccess)
        {
            return Result<TutorChatResponseDto>.Failure(
                tutorLimitCheck.ErrorCode!,
                tutorLimitCheck.ErrorMessage!);
        }

        var config = await _context.AIProviderConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UsageType == AIUsageType.Assistant && c.IsActive, cancellationToken);

        if (config == null)
        {
            return Result<TutorChatResponseDto>.Failure("AI_CONFIG_NOT_FOUND", "AI assistant config not found");
        }

        Conversation? conversation = null;
        if (request.ConversationId.HasValue)
        {
            conversation = await _context.Conversations
                .FirstOrDefaultAsync(c => c.ConversationId == request.ConversationId && c.UserId == userId && !c.IsDeleted, cancellationToken);

            if (conversation == null)
            {
                return Result<TutorChatResponseDto>.Failure("CONVERSATION_NOT_FOUND", "Conversation not found");
            }
        }
        else
        {
            conversation = await FindConversationByContextAsync(userId, request, cancellationToken);
        }

        var contextIds = ResolveContextIds(request, conversation);
        var contextData = await LoadTutorContextAsync(userId, contextIds.LearningPathId, contextIds.ChapterId, contextIds.LessonId, cancellationToken);
        if (!contextData.IsSuccess)
        {
            return Result<TutorChatResponseDto>.Failure(contextData.ErrorCode!, contextData.ErrorMessage!);
        }

        if (conversation == null)
        {
            conversation = new Conversation
            {
                ConversationId = NewId.NextGuid(),
                UserId = userId,
                ConfigId = config.ConfigId,
                Title = BuildConversationTitle(contextData.Value!, request.Message),
                CreatedAt = DateTime.UtcNow,
                MessageCount = 0,
                IsDeleted = false,
                LearningPathId = contextIds.LearningPathId,
                ChapterId = contextIds.ChapterId,
                LessonId = contextIds.LessonId
            };
            await _context.Conversations.AddAsync(conversation, cancellationToken);
        }
        else if (contextIds.LearningPathId.HasValue || contextIds.ChapterId.HasValue || contextIds.LessonId.HasValue)
        {
            if (!conversation.LearningPathId.HasValue && !conversation.ChapterId.HasValue && !conversation.LessonId.HasValue)
            {
                conversation.LearningPathId = contextIds.LearningPathId;
                conversation.ChapterId = contextIds.ChapterId;
                conversation.LessonId = contextIds.LessonId;
            }
        }

        var history = await _context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversation.ConversationId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(6)
            .Select(m => m.Content)
            .ToListAsync(cancellationToken);

        history.Reverse();

        var prompt = BuildTutorPrompt(
            contextData.Value!,
            request.Message.Trim(),
            history);

        string assistantReply;
        try
        {
            assistantReply = await _aiGeneratorService.GenerateContentAsync(prompt, AIUsageType.Assistant);
        }
        catch (Exception ex)
        {
            return Result<TutorChatResponseDto>.Failure("AI_RESPONSE_FAILED", ex.Message);
        }

        var userMessage = new Message
        {
            MessageId = NewId.NextGuid(),
            ConversationId = conversation.ConversationId,
            Content = $"USER: {request.Message.Trim()}",
            CreatedAt = DateTime.UtcNow
        };

        var assistantMessage = new Message
        {
            MessageId = NewId.NextGuid(),
            ConversationId = conversation.ConversationId,
            Content = $"ASSISTANT: {assistantReply.Trim()}",
            CreatedAt = DateTime.UtcNow
        };

        await _context.Messages.AddAsync(userMessage, cancellationToken);
        await _context.Messages.AddAsync(assistantMessage, cancellationToken);

        conversation.MessageCount += 2;

        await _context.SaveChangesAsync(cancellationToken);

        return Result<TutorChatResponseDto>.Success(new TutorChatResponseDto(
            conversation.ConversationId,
            userMessage.MessageId,
            assistantMessage.MessageId,
            assistantReply.Trim(),
            assistantMessage.CreatedAt
        ));
    }

    private static string BuildConversationTitle(TutorContext context, string message)
    {
        if (!string.IsNullOrWhiteSpace(context.LessonTitle))
            return $"Tutor - {context.LessonTitle}";
        if (!string.IsNullOrWhiteSpace(context.ChapterTitle))
            return $"Tutor - {context.ChapterTitle}";
        if (!string.IsNullOrWhiteSpace(context.LearningPathTitle))
            return $"Tutor - {context.LearningPathTitle}";

        var trimmed = message.Trim();
        if (trimmed.Length <= 60)
            return trimmed;

        return trimmed.Substring(0, 60) + "...";
    }

    private async Task<Result<TutorContext>> LoadTutorContextAsync(
        Guid userId,
        Guid? learningPathId,
        Guid? chapterId,
        Guid? lessonId,
        CancellationToken cancellationToken)
    {
        LearningPath? learningPath = null;
        Chapter? chapter = null;
        Lesson? lesson = null;

        if (lessonId.HasValue)
        {
            lesson = await _context.Lessons
                .Include(l => l.Chapter)
                    .ThenInclude(c => c.LearningPath)
                        .ThenInclude(lp => lp.Subject)
                .Include(l => l.Chapter)
                    .ThenInclude(c => c.LearningPath)
                        .ThenInclude(lp => lp.LearningPathGoals)
                            .ThenInclude(lpg => lpg.Goal)
                .FirstOrDefaultAsync(l => l.LessonId == lessonId, cancellationToken);

            if (lesson == null)
                return Result<TutorContext>.Failure("LESSON_NOT_FOUND", "Lesson not found");

            chapter = lesson.Chapter;
            learningPath = chapter.LearningPath;
        }
        else if (chapterId.HasValue)
        {
            chapter = await _context.Chapters
                .Include(c => c.LearningPath)
                    .ThenInclude(lp => lp.Subject)
                .Include(c => c.LearningPath)
                    .ThenInclude(lp => lp.LearningPathGoals)
                        .ThenInclude(lpg => lpg.Goal)
                .FirstOrDefaultAsync(c => c.ChapterId == chapterId, cancellationToken);

            if (chapter == null)
                return Result<TutorContext>.Failure("CHAPTER_NOT_FOUND", "Chapter not found");

            learningPath = chapter.LearningPath;
        }
        else if (learningPathId.HasValue)
        {
            learningPath = await _context.LearningPaths
                .Include(lp => lp.Subject)
                .Include(lp => lp.LearningPathGoals)
                    .ThenInclude(lpg => lpg.Goal)
                .FirstOrDefaultAsync(lp => lp.PathId == learningPathId, cancellationToken);

            if (learningPath == null)
                return Result<TutorContext>.Failure("LEARNING_PATH_NOT_FOUND", "Learning path not found");
        }

        if (learningPath != null && learningPath.UserId != userId)
        {
            return Result<TutorContext>.Failure("ACCESS_DENIED", "You do not have access to this learning path");
        }

        var goals = learningPath?.LearningPathGoals
            .OrderByDescending(g => g.Weight)
            .Select(g => $"{g.Goal.Title} ({g.Weight:P0})")
            .ToList() ?? new List<string>();

        return Result<TutorContext>.Success(new TutorContext(
            learningPath?.Subject.Name,
            learningPath?.Title,
            learningPath?.Description,
            learningPath?.Language ?? LanguageSelection.VietNamese,
            goals,
            chapter?.Title,
            lesson?.Title,
            lesson?.Content
        ));
    }

    private static string BuildTutorPrompt(TutorContext context, string userMessage, List<string> history)
    {
        var historyText = history.Count == 0
            ? "No prior messages."
            : string.Join("\n", history);

        var languageInstruction = context.Language switch
        {
            LanguageSelection.VietNamese => "Respond in Vietnamese, keep technical terms in English.",
            LanguageSelection.English => "Respond in English.",
            _ => "Respond in Vietnamese."
        };

        return $@"You are a friendly tutor helping a student follow their learning path.
{languageInstruction}

CONTEXT:
Subject: {context.SubjectName ?? "N/A"}
Learning Path: {context.LearningPathTitle ?? "N/A"}
Learning Path Description: {context.LearningPathDescription ?? "N/A"}
Goals: {(context.Goals.Count == 0 ? "N/A" : string.Join(", ", context.Goals))}
Current Chapter: {context.ChapterTitle ?? "N/A"}
Current Lesson: {context.LessonTitle ?? "N/A"}
Lesson Content: {context.LessonContent ?? "N/A"}

CONVERSATION HISTORY:
{historyText}

USER QUESTION:
{userMessage}

INSTRUCTIONS:
- Focus on the current subject and lesson.
- Explain clearly and concisely.
- Provide short examples when helpful.
- If the question is out of scope, gently suggest focusing on the current lesson.";
    }

    private sealed record TutorContext(
        string? SubjectName,
        string? LearningPathTitle,
        string? LearningPathDescription,
        LanguageSelection Language,
        List<string> Goals,
        string? ChapterTitle,
        string? LessonTitle,
        string? LessonContent
    );

    private static (Guid? LearningPathId, Guid? ChapterId, Guid? LessonId) ResolveContextIds(
        SendTutorMessageCommand request,
        Conversation? conversation)
    {
        if (request.LessonId.HasValue)
            return (null, null, request.LessonId);
        if (request.ChapterId.HasValue)
            return (null, request.ChapterId, null);
        if (request.LearningPathId.HasValue)
            return (request.LearningPathId, null, null);

        if (conversation?.LessonId.HasValue == true)
            return (null, null, conversation.LessonId);
        if (conversation?.ChapterId.HasValue == true)
            return (null, conversation.ChapterId, null);
        if (conversation?.LearningPathId.HasValue == true)
            return (conversation.LearningPathId, null, null);

        return (null, null, null);
    }

    private async Task<Conversation?> FindConversationByContextAsync(
        Guid userId,
        SendTutorMessageCommand request,
        CancellationToken cancellationToken)
    {
        if (request.LessonId.HasValue)
        {
            return await _context.Conversations
                .FirstOrDefaultAsync(c => c.UserId == userId && c.LessonId == request.LessonId && !c.IsDeleted, cancellationToken);
        }

        if (request.ChapterId.HasValue)
        {
            return await _context.Conversations
                .FirstOrDefaultAsync(c => c.UserId == userId && c.ChapterId == request.ChapterId && !c.IsDeleted, cancellationToken);
        }

        if (request.LearningPathId.HasValue)
        {
            return await _context.Conversations
                .FirstOrDefaultAsync(c => c.UserId == userId && c.LearningPathId == request.LearningPathId && !c.IsDeleted, cancellationToken);
        }

        return null;
    }
}
