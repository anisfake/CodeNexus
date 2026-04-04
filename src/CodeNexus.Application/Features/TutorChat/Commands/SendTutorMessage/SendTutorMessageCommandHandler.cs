using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.TutorChat.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace CodeNexus.Application.Features.TutorChat.Commands.SendTutorMessage;

public class SendTutorMessageCommandHandler : IRequestHandler<SendTutorMessageCommand, Result<TutorChatResponseDto>>
{
    private const int RecentHistoryWindowSize = 12;
    private const int OlderHistoryDigestWindowSize = 18;
    private const int DigestLineLimit = 8;
    private const int HistoryMessageCharLimit = 260;
    private const int LearningPathDescriptionCharLimit = 600;
    private const int ChapterContentCharLimit = 1800;
    private const int LessonContentCharLimit = 2200;

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
        var normalizedUserMessage = request.Message.Trim();

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
                return Result<TutorChatResponseDto>.Failure("CONVERSATION_NOT_FOUND", "Conversation not found.");
            }
        }

        var requestedContextIds = ResolveRequestedContextIds(request, conversation);
        var contextData = await LoadTutorContextAsync(
            userId,
            requestedContextIds.LearningPathId,
            requestedContextIds.ChapterId,
            requestedContextIds.LessonId,
            cancellationToken);

        if (!contextData.IsSuccess)
        {
            return Result<TutorChatResponseDto>.Failure(contextData.ErrorCode!, contextData.ErrorMessage!);
        }

        if (conversation == null)
        {
            conversation = await FindConversationByContextAsync(userId, contextData.Value!, cancellationToken);
        }

        if (conversation != null)
        {
            var chapterScopeValidation = await ValidateConversationChapterScopeAsync(conversation, contextData.Value!, cancellationToken);
            if (!chapterScopeValidation.IsSuccess)
            {
                return Result<TutorChatResponseDto>.Failure(
                    chapterScopeValidation.ErrorCode!,
                    chapterScopeValidation.ErrorMessage!);
            }
        }

        if (conversation == null)
        {
            conversation = new Conversation
            {
                ConversationId = NewId.NextGuid(),
                UserId = userId,
                ConfigId = config.ConfigId,
                Title = BuildConversationTitle(contextData.Value!, normalizedUserMessage),
                CreatedAt = DateTime.UtcNow,
                MessageCount = 0,
                IsDeleted = false,
                LearningPathId = contextData.Value!.LearningPathId,
                ChapterId = contextData.Value!.ChapterId,
                // Chapter is the session boundary, lesson is only message-level context.
                LessonId = contextData.Value!.ChapterId.HasValue ? null : contextData.Value!.LessonId
            };
            await _context.Conversations.AddAsync(conversation, cancellationToken);
        }
        else
        {
            ApplyConversationContext(conversation, contextData.Value!);
        }

        var historySnapshot = await LoadConversationHistorySnapshotAsync(conversation.ConversationId, cancellationToken);

        var prompt = BuildTutorPrompt(
            contextData.Value!,
            normalizedUserMessage,
            historySnapshot);

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
            Content = $"USER: {normalizedUserMessage}",
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
        if (!string.IsNullOrWhiteSpace(context.ChapterTitle))
            return $"Tutor - {context.ChapterTitle}";
        if (!string.IsNullOrWhiteSpace(context.LearningPathTitle))
            return $"Tutor - {context.LearningPathTitle}";
        if (!string.IsNullOrWhiteSpace(context.LessonTitle))
            return $"Tutor - {context.LessonTitle}";

        var trimmed = message.Trim();
        if (trimmed.Length <= 60)
            return trimmed;

        return trimmed.Substring(0, 60) + "...";
    }

    private async Task<Result> ValidateConversationChapterScopeAsync(
        Conversation conversation,
        TutorContext context,
        CancellationToken cancellationToken)
    {
        if (!context.ChapterId.HasValue)
        {
            return Result.Success();
        }

        var conversationChapterId = conversation.ChapterId;
        if (!conversationChapterId.HasValue && conversation.LessonId.HasValue)
        {
            conversationChapterId = await _context.Lessons
                .AsNoTracking()
                .Where(l => l.LessonId == conversation.LessonId.Value)
                .Select(l => (Guid?)l.ChapterId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (conversationChapterId.HasValue && conversationChapterId.Value != context.ChapterId.Value)
        {
            return Result.Failure(
                "CONVERSATION_CONTEXT_MISMATCH",
                "This conversation belongs to a different chapter. Please resolve a new chapter conversation.");
        }

        return Result.Success();
    }

    private static void ApplyConversationContext(Conversation conversation, TutorContext context)
    {
        if (context.ChapterId.HasValue)
        {
            conversation.LearningPathId = context.LearningPathId;
            conversation.ChapterId = context.ChapterId;
            conversation.LessonId = null;
            return;
        }

        if (context.LearningPathId.HasValue && !conversation.ChapterId.HasValue && !conversation.LearningPathId.HasValue)
        {
            conversation.LearningPathId = context.LearningPathId;
        }
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

        if (chapterId.HasValue)
        {
            chapter = await _context.Chapters
                .Include(c => c.LearningPath)
                    .ThenInclude(lp => lp.Subject)
                .Include(c => c.LearningPath)
                    .ThenInclude(lp => lp.LearningPathGoals)
                        .ThenInclude(lpg => lpg.Goal)
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
                        .ThenInclude(lp => lp.Subject)
                .Include(l => l.Chapter)
                    .ThenInclude(c => c.LearningPath)
                        .ThenInclude(lp => lp.LearningPathGoals)
                            .ThenInclude(lpg => lpg.Goal)
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
                .Include(lp => lp.Subject)
                .Include(lp => lp.LearningPathGoals)
                    .ThenInclude(lpg => lpg.Goal)
                .FirstOrDefaultAsync(lp => lp.PathId == learningPathId, cancellationToken);

            if (learningPath == null)
                return Result<TutorContext>.Failure("LEARNING_PATH_NOT_FOUND", "Learning path not found.");
        }

        if (learningPath != null && learningPath.UserId != userId)
        {
            return Result<TutorContext>.Failure("ACCESS_DENIED", "Access denied.");
        }

        var goals = learningPath?.LearningPathGoals
            .OrderByDescending(g => g.Weight)
            .Select(g => $"{g.Goal.Title} ({g.Weight:P0})")
            .ToList() ?? new List<string>();

        var chapterLessonTitles = new List<string>();
        if (chapter != null)
        {
            chapterLessonTitles = await _context.Lessons
                .AsNoTracking()
                .Where(l => l.ChapterId == chapter.ChapterId && !l.IsDeleted)
                .OrderBy(l => l.OrderIndex)
                .Select(l => $"{l.OrderIndex}. {l.Title}")
                .ToListAsync(cancellationToken);
        }

        return Result<TutorContext>.Success(new TutorContext(
            learningPath?.Subject?.Name,
            learningPath?.Title,
            learningPath?.Description,
            learningPath?.Language ?? LanguageSelection.VietNamese,
            goals,
            learningPath?.PathId,
            chapter?.ChapterId,
            chapter?.Title,
            chapter?.Content,
            chapterLessonTitles,
            lesson?.LessonId,
            lesson?.Title,
            lesson?.Content
        ));
    }

    private async Task<ConversationHistorySnapshot> LoadConversationHistorySnapshotAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var totalMessages = await _context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .CountAsync(cancellationToken);

        var recentMessages = await _context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(RecentHistoryWindowSize)
            .Select(m => m.Content)
            .ToListAsync(cancellationToken);

        recentMessages.Reverse();

        var compactRecent = recentMessages
            .Select(message => CompactMessage(message, HistoryMessageCharLimit))
            .ToList();

        if (totalMessages <= RecentHistoryWindowSize)
        {
            return new ConversationHistorySnapshot(totalMessages, compactRecent, null);
        }

        var olderMessages = await _context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderByDescending(m => m.CreatedAt)
            .Skip(RecentHistoryWindowSize)
            .Take(OlderHistoryDigestWindowSize)
            .Select(m => m.Content)
            .ToListAsync(cancellationToken);

        olderMessages.Reverse();

        var olderSummary = BuildOlderHistorySummary(
            olderMessages,
            Math.Max(totalMessages - RecentHistoryWindowSize, 0));

        return new ConversationHistorySnapshot(totalMessages, compactRecent, olderSummary);
    }

    private static string BuildTutorPrompt(TutorContext context, string userMessage, ConversationHistorySnapshot historySnapshot)
    {
        var historyText = historySnapshot.RecentMessages.Count == 0
            ? "No prior messages."
            : string.Join("\n", historySnapshot.RecentMessages);

        var languageInstruction = context.Language switch
        {
            LanguageSelection.VietNamese => "Respond in Vietnamese, keep technical terms in English.",
            LanguageSelection.English => "Respond in English.",
            _ => "Respond in Vietnamese."
        };

        var chapterLessonOutline = context.ChapterLessonTitles.Count == 0
            ? "N/A"
            : string.Join("\n", context.ChapterLessonTitles.Select(title => $"- {title}"));

        var learningPathDescription = ClipContent(context.LearningPathDescription, LearningPathDescriptionCharLimit);
        var chapterContent = ClipContent(context.ChapterContent, ChapterContentCharLimit);
        var lessonContent = ClipContent(context.LessonContent, LessonContentCharLimit);
        var goals = context.Goals.Count == 0
            ? "N/A"
            : string.Join(", ", context.Goals.Take(5));

        var olderConversationSummary = string.IsNullOrWhiteSpace(historySnapshot.OlderSummary)
            ? "N/A"
            : historySnapshot.OlderSummary;

        return $@"You are a friendly tutor helping a student follow their learning path.
{languageInstruction}

CONTEXT:
Subject: {context.SubjectName ?? "N/A"}
Learning Path: {context.LearningPathTitle ?? "N/A"}
Learning Path Description (summary): {learningPathDescription}
Goals: {goals}
Current Chapter: {context.ChapterTitle ?? "N/A"}
Chapter Content (summary): {chapterContent}
Chapter Lessons:
{chapterLessonOutline}
Active Lesson: {context.LessonTitle ?? "N/A"}
Active Lesson Content (summary): {lessonContent}

CONVERSATION HISTORY:
Conversation Length: {historySnapshot.TotalMessages} messages in this chapter session.

Older Messages Summary:
{olderConversationSummary}

Recent Messages (most relevant):
{historyText}

USER QUESTION:
{userMessage}

INSTRUCTIONS:
- Focus on the current chapter by default.
- If Active Lesson is available, prioritize that lesson for concrete details.
- If the question spans multiple lessons in this chapter, synthesize them clearly.
- Keep answers concise by default (target 6-10 bullet points or around 150-250 words).
- Provide short examples when helpful.
- If the question references previous lessons, connect the answer to recent chapter history before explaining new content.
- If the question is out of this chapter's scope, gently ask the student to switch to the correct chapter conversation.
- If details are missing due to summarized context, ask for a short excerpt from the lesson content before answering deeply.";
    }

    private static string BuildOlderHistorySummary(List<string> olderMessages, int olderMessageCount)
    {
        if (olderMessages.Count == 0 || olderMessageCount <= 0)
        {
            return "N/A";
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Older context ({olderMessageCount} earlier messages) in compact form:");

        foreach (var line in olderMessages.Take(DigestLineLimit))
        {
            builder.Append("- ");
            builder.AppendLine(CompactMessage(line, 180));
        }

        if (olderMessages.Count > DigestLineLimit)
        {
            builder.AppendLine("- ...");
        }

        return builder.ToString().Trim();
    }

    private static string CompactMessage(string content, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var normalized = string.Join(" ", content
            .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));

        if (normalized.Length <= maxChars)
        {
            return normalized;
        }

        return normalized.Substring(0, maxChars) + "...";
    }

    private static string ClipContent(string? content, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return "N/A";
        }

        var trimmed = content.Trim();
        if (trimmed.Length <= maxChars)
        {
            return trimmed;
        }

        return trimmed.Substring(0, maxChars) + "...";
    }

    private sealed record TutorContext(
        string? SubjectName,
        string? LearningPathTitle,
        string? LearningPathDescription,
        LanguageSelection Language,
        List<string> Goals,
        Guid? LearningPathId,
        Guid? ChapterId,
        string? ChapterTitle,
        string? ChapterContent,
        List<string> ChapterLessonTitles,
        Guid? LessonId,
        string? LessonTitle,
        string? LessonContent
    );

    private sealed record ConversationHistorySnapshot(
        int TotalMessages,
        List<string> RecentMessages,
        string? OlderSummary);

    private static (Guid? LearningPathId, Guid? ChapterId, Guid? LessonId) ResolveRequestedContextIds(
        SendTutorMessageCommand request,
        Conversation? conversation)
    {
        if (request.LessonId.HasValue)
            return (null, null, request.LessonId);
        if (request.ChapterId.HasValue)
            return (null, request.ChapterId, null);
        if (request.LearningPathId.HasValue)
            return (request.LearningPathId, null, null);

        if (conversation?.ChapterId.HasValue == true)
            return (conversation.LearningPathId, conversation.ChapterId, null);
        if (conversation?.LessonId.HasValue == true)
            return (null, null, conversation.LessonId);
        if (conversation?.LearningPathId.HasValue == true)
            return (conversation.LearningPathId, null, null);

        return (null, null, null);
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
}
