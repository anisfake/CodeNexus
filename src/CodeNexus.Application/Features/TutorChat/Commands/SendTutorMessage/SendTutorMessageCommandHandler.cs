using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.TutorChat;
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
    private const string DefaultAssistantModel = "meta-llama/llama-4-scout-17b-16e-instruct";

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAIGeneratorService _aiGeneratorService;
    private readonly IPlanUsageLimitService _planUsageLimitService;
    private readonly ISubscriptionAccessService _subscriptionAccessService;

    public SendTutorMessageCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IAIGeneratorService aiGeneratorService,
        IPlanUsageLimitService planUsageLimitService,
        ISubscriptionAccessService subscriptionAccessService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _aiGeneratorService = aiGeneratorService;
        _planUsageLimitService = planUsageLimitService;
        _subscriptionAccessService = subscriptionAccessService;
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

        var config = await ResolveActiveAssistantConfigAsync(userId, cancellationToken);

        if (config == null)
        {
            return Result<TutorChatResponseDto>.Failure("AI_CONFIG_NOT_FOUND", "AI assistant config not found");
        }
        var chatPolicy = TutorChatRuntimePolicy.Resolve(config.ConfigJson);
        var modelTokenBudget = ResolveModelTokenBudget(config, chatPolicy);

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

        if (conversation.ConfigId != config.ConfigId)
        {
            conversation.ConfigId = config.ConfigId;
        }

        await EnsureConversationSummaryWithinModelBudgetAsync(
            conversation,
            contextData.Value!,
            normalizedUserMessage,
            modelTokenBudget,
            chatPolicy,
            cancellationToken);

        var historySnapshot = await LoadConversationHistorySnapshotAsync(
            conversation.ConversationId,
            contextData.Value!,
            normalizedUserMessage,
            modelTokenBudget,
            chatPolicy,
            cancellationToken);

        var prompt = BuildTutorPrompt(
            contextData.Value!,
            normalizedUserMessage,
            historySnapshot,
            chatPolicy);
        var contextUsagePercent = CalculateContextUsagePercent(
            historySnapshot.EstimatedPromptTokens,
            modelTokenBudget.ContextWindow);

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

        await _planUsageLimitService.RecordTutorMessageUsageAsync(userId, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<TutorChatResponseDto>.Success(new TutorChatResponseDto(
            conversation.ConversationId,
            userMessage.MessageId,
            assistantMessage.MessageId,
            assistantReply.Trim(),
            assistantMessage.CreatedAt,
            contextUsagePercent
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

    private async Task<AIProviderConfig?> ResolveActiveAssistantConfigAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var userAccess = await _context.Users
            .AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => new
            {
                RoleName = u.Role != null ? u.Role.RoleName : string.Empty,
                u.TokenBalance
            })
            .FirstOrDefaultAsync(cancellationToken);

        var preferredTier = ResolvePreferredTier(userAccess?.RoleName, userAccess?.TokenBalance ?? 0m);

        var preferred = await _context.AIProviderConfigs
            .AsNoTracking()
            .Where(c =>
                c.UsageType == AIUsageType.Assistant
                && c.AccessTier == preferredTier
                && c.IsActive)
            .OrderByDescending(c => c.LastUpdated)
            .ThenBy(c => c.ConfigId)
            .FirstOrDefaultAsync(cancellationToken);

        if (preferred != null)
        {
            return preferred;
        }

        var fallbackTier = preferredTier == AIAccessTier.Paid ? AIAccessTier.Free : AIAccessTier.Paid;

        var fallback = await _context.AIProviderConfigs
            .AsNoTracking()
            .Where(c =>
                c.UsageType == AIUsageType.Assistant
                && c.AccessTier == fallbackTier
                && c.IsActive)
            .OrderByDescending(c => c.LastUpdated)
            .ThenBy(c => c.ConfigId)
            .FirstOrDefaultAsync(cancellationToken);

        if (fallback != null)
        {
            return fallback;
        }

        return await _context.AIProviderConfigs
            .AsNoTracking()
            .Where(c => c.UsageType == AIUsageType.Assistant && c.IsActive)
            .OrderByDescending(c => c.LastUpdated)
            .ThenBy(c => c.ConfigId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static AIAccessTier ResolvePreferredTier(string? roleName, decimal tokenBalance)
    {
        if (string.Equals(roleName, "Admin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(roleName, "Mentor", StringComparison.OrdinalIgnoreCase))
        {
            return AIAccessTier.Paid;
        }

        return tokenBalance > 0m ? AIAccessTier.Paid : AIAccessTier.Free;
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

    private async Task EnsureConversationSummaryWithinModelBudgetAsync(
        Conversation conversation,
        TutorContext context,
        string userMessage,
        ModelTokenBudget tokenBudget,
        TutorChatPolicy chatPolicy,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var latestSummaryEndAt = await _context.ConversationSummaries
                .AsNoTracking()
                .Where(s => s.ConversationId == conversation.ConversationId)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => s.EndMessageCreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            var unsummarizedMessagesQuery = _context.Messages
                .AsNoTracking()
                .Where(m => m.ConversationId == conversation.ConversationId);

            if (latestSummaryEndAt.HasValue)
            {
                var boundary = latestSummaryEndAt.Value;
                unsummarizedMessagesQuery = unsummarizedMessagesQuery
                    .Where(m => m.CreatedAt > boundary);
            }

            var unsummarizedMessages = await unsummarizedMessagesQuery
                .OrderBy(m => m.CreatedAt)
                .ThenBy(m => m.MessageId)
                .ToListAsync(cancellationToken);

            if (unsummarizedMessages.Count == 0)
            {
                return;
            }

            var archivedSummaries = await _context.ConversationSummaries
                .AsNoTracking()
                .Where(s => s.ConversationId == conversation.ConversationId)
                .OrderByDescending(s => s.CreatedAt)
                .Take(chatPolicy.ArchivedSummaryPromptTake)
                .OrderBy(s => s.CreatedAt)
                .Select(s => s.SummaryContent)
                .ToListAsync(cancellationToken);

            var estimatedInputTokens = EstimateConversationInputTokens(
                context,
                userMessage,
                unsummarizedMessages,
                archivedSummaries,
                chatPolicy);

            if (estimatedInputTokens < tokenBudget.SummaryTriggerBudget)
            {
                return;
            }

            var reachedForceSummary = estimatedInputTokens >= tokenBudget.ForceSummaryBudget;
            var preserveRecent = reachedForceSummary
                ? chatPolicy.ForceSummaryPreserveRecentMessages
                : chatPolicy.SummaryPreserveRecentMessages;

            if (!reachedForceSummary
                && (unsummarizedMessages.Count < chatPolicy.SummaryMinUnsummarizedMessages
                    || unsummarizedMessages.Count <= preserveRecent))
            {
                return;
            }

            preserveRecent = Math.Min(preserveRecent, unsummarizedMessages.Count - 1);
            var summarizeCount = unsummarizedMessages.Count - preserveRecent;
            if (summarizeCount <= 0)
            {
                return;
            }

            var messagesToSummarize = unsummarizedMessages
                .Take(summarizeCount)
                .ToList();

            if (messagesToSummarize.Count == 0)
            {
                return;
            }

            var archiveSummary = new ConversationSummary
            {
                SummaryId = NewId.NextGuid(),
                ConversationId = conversation.ConversationId,
                MessageCount = messagesToSummarize.Count,
                StartMessageCreatedAt = messagesToSummarize.First().CreatedAt,
                EndMessageCreatedAt = messagesToSummarize.Last().CreatedAt,
                SummaryContent = BuildArchivedSummaryContent(messagesToSummarize, chatPolicy),
                CreatedAt = DateTime.UtcNow
            };

            await _context.ConversationSummaries.AddAsync(archiveSummary, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
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

        var learningPathChapterTitles = new List<string>();
        if (learningPath != null)
        {
            learningPathChapterTitles = await _context.Chapters
                .AsNoTracking()
                .Where(c => c.PathId == learningPath.PathId && !c.IsDeleted)
                .OrderBy(c => c.OrderIndex)
                .Select(c => $"{c.OrderIndex}. {c.Title}")
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
            learningPathChapterTitles,
            chapterLessonTitles,
            lesson?.LessonId,
            lesson?.Title,
            lesson?.Content
        ));
    }

    private async Task<ConversationHistorySnapshot> LoadConversationHistorySnapshotAsync(
        Guid conversationId,
        TutorContext context,
        string userMessage,
        ModelTokenBudget tokenBudget,
        TutorChatPolicy chatPolicy,
        CancellationToken cancellationToken)
    {
        var totalMessages = await _context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .CountAsync(cancellationToken);

        var archivedSummaries = await _context.ConversationSummaries
            .AsNoTracking()
            .Where(s => s.ConversationId == conversationId)
            .OrderByDescending(s => s.CreatedAt)
            .Take(chatPolicy.ArchivedSummaryPromptTake)
            .OrderBy(s => s.CreatedAt)
            .Select(s => s.SummaryContent)
            .ToListAsync(cancellationToken);

        var latestSummaryEndAt = await _context.ConversationSummaries
            .AsNoTracking()
            .Where(s => s.ConversationId == conversationId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => s.EndMessageCreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var activeMessagesQuery = _context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId);

        if (latestSummaryEndAt.HasValue)
        {
            var boundary = latestSummaryEndAt.Value;
            activeMessagesQuery = activeMessagesQuery
                .Where(m => m.CreatedAt > boundary);
        }

        var activeMessages = await activeMessagesQuery
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.MessageId)
            .Select(m => m.Content)
            .ToListAsync(cancellationToken);

        if (activeMessages.Count == 0)
        {
            var onlySummary = BuildOlderHistorySummary(new List<string>(), archivedSummaries, 0, 0, chatPolicy);
            var emptySnapshot = new ConversationHistorySnapshot(totalMessages, new List<string>(), onlySummary, 0);
            var estimatedEmptyTokens = EstimateTokenCount(BuildTutorPrompt(context, userMessage, emptySnapshot, chatPolicy));
            return emptySnapshot with { EstimatedPromptTokens = estimatedEmptyTokens };
        }

        var absoluteMinRecent = Math.Min(chatPolicy.RecentHistoryMinMessages, activeMessages.Count);

        var recentCount = Math.Min(chatPolicy.RecentHistoryMaxMessages, activeMessages.Count);
        var recentCharLimit = chatPolicy.HistoryMessageCharLimit;
        var olderLineLimit = chatPolicy.OlderDigestInitialLineLimit;
        var olderCharLimit = chatPolicy.OlderHistoryCharLimit;

        var snapshot = BuildSnapshot(
            activeMessages,
            archivedSummaries,
            totalMessages,
            recentCount,
            recentCharLimit,
            olderLineLimit,
            olderCharLimit,
            chatPolicy);

        var estimatedTokens = EstimateTokenCount(BuildTutorPrompt(context, userMessage, snapshot, chatPolicy));

        while (estimatedTokens > tokenBudget.PromptInputBudget)
        {
            if (olderLineLimit > 0)
            {
                olderLineLimit = Math.Max(0, olderLineLimit - chatPolicy.OlderDigestStep);
            }
            else if (recentCount > absoluteMinRecent)
            {
                recentCount--;
            }
            else if (recentCharLimit > chatPolicy.HistoryMessageMinCharLimit)
            {
                recentCharLimit = Math.Max(chatPolicy.HistoryMessageMinCharLimit, recentCharLimit - 20);
            }
            else if (olderCharLimit > chatPolicy.OlderHistoryMinCharLimit)
            {
                olderCharLimit = Math.Max(chatPolicy.OlderHistoryMinCharLimit, olderCharLimit - 20);
            }
            else
            {
                break;
            }

            snapshot = BuildSnapshot(
                activeMessages,
                archivedSummaries,
                totalMessages,
                recentCount,
                recentCharLimit,
                olderLineLimit,
                olderCharLimit,
                chatPolicy);

            estimatedTokens = EstimateTokenCount(BuildTutorPrompt(context, userMessage, snapshot, chatPolicy));
        }

        return snapshot with { EstimatedPromptTokens = estimatedTokens };
    }

    private static string BuildTutorPrompt(
        TutorContext context,
        string userMessage,
        ConversationHistorySnapshot historySnapshot,
        TutorChatPolicy chatPolicy)
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

        var chapterPathOutline = context.LearningPathChapterTitles.Count == 0
            ? "N/A"
            : string.Join("\n", context.LearningPathChapterTitles.Select(title => $"- {title}"));

        var learningPathDescription = ClipContent(context.LearningPathDescription, chatPolicy.LearningPathDescriptionCharLimit);
        var chapterContent = ClipContent(context.ChapterContent, chatPolicy.ChapterContentCharLimit);
        var lessonContent = ClipContent(context.LessonContent, chatPolicy.LessonContentCharLimit);
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
Learning Path Chapters (exact titles):
{chapterPathOutline}

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
- If you mention a chapter by name/index, only use titles from 'Learning Path Chapters (exact titles)'.
- Never invent chapter titles, chapter indexes, or roadmap structure not present in provided context.
- If details are missing due to summarized context, ask for a short excerpt from the lesson content before answering deeply.";
    }

    private static ConversationHistorySnapshot BuildSnapshot(
        List<string> activeMessages,
        List<string> archivedSummaries,
        int totalMessages,
        int recentCount,
        int recentCharLimit,
        int olderLineLimit,
        int olderCharLimit,
        TutorChatPolicy chatPolicy)
    {
        var safeRecentCount = Math.Min(Math.Max(recentCount, 0), activeMessages.Count);
        var splitIndex = Math.Max(activeMessages.Count - safeRecentCount, 0);

        var olderRaw = activeMessages.Take(splitIndex).ToList();
        var recentRaw = activeMessages.Skip(splitIndex).ToList();

        var recentMessages = recentRaw
            .Select(content => CompactMessage(content, recentCharLimit))
            .ToList();

        // After summaries exist, prompt should rely on summary + recent turns.
        var olderDigestSource = archivedSummaries.Count > 0 ? new List<string>() : olderRaw;
        var olderSummary = BuildOlderHistorySummary(
            olderDigestSource,
            archivedSummaries,
            olderLineLimit,
            olderCharLimit,
            chatPolicy);

        return new ConversationHistorySnapshot(totalMessages, recentMessages, olderSummary, 0);
    }

    private static string BuildOlderHistorySummary(
        List<string> olderMessages,
        List<string> archivedSummaries,
        int lineLimit,
        int lineCharLimit,
        TutorChatPolicy chatPolicy)
    {
        if (olderMessages.Count == 0 && archivedSummaries.Count == 0)
        {
            return "N/A";
        }

        var safeLineLimit = Math.Max(lineLimit, 0);
        var digestLines = safeLineLimit == 0
            ? new List<string>()
            : olderMessages
                .TakeLast(Math.Min(safeLineLimit, olderMessages.Count))
                .Select(content => CompactMessage(content, lineCharLimit))
                .ToList();

        var summarizedHiddenCount = Math.Max(olderMessages.Count - digestLines.Count, 0);

        var builder = new StringBuilder();
        builder.AppendLine("Older context compressed:");

        if (archivedSummaries.Count > 0)
        {
            builder.AppendLine($"- {archivedSummaries.Count} archived summary blocks are available.");
            foreach (var archived in archivedSummaries)
            {
                builder.Append("- [Archived] ");
                builder.AppendLine(CompactMessage(archived, chatPolicy.ArchivedSummaryPromptCharLimit));
            }
        }

        if (summarizedHiddenCount > 0)
        {
            builder.AppendLine($"- {summarizedHiddenCount} additional older messages are summarized.");
        }

        if (digestLines.Count == 0)
        {
            builder.AppendLine("- Older details trimmed to keep token budget safe.");
        }
        else
        {
            foreach (var line in digestLines)
            {
                builder.Append("- ");
                builder.AppendLine(line);
            }
        }

        return builder.ToString().Trim();
    }

    private static string BuildArchivedSummaryContent(List<Message> messagesToArchive, TutorChatPolicy chatPolicy)
    {
        var userCount = messagesToArchive.Count(m => ParseRole(m.Content) == "user");
        var assistantCount = messagesToArchive.Count(m => ParseRole(m.Content) == "assistant");

        var head = messagesToArchive
            .Take(chatPolicy.ArchiveSummaryTakeHeadLines)
            .Select(m => $"[{ParseRole(m.Content)}] {CompactMessage(StripRolePrefix(m.Content), chatPolicy.ArchiveSummaryLineCharLimit)}")
            .ToList();

        var tail = messagesToArchive
            .TakeLast(chatPolicy.ArchiveSummaryTakeTailLines)
            .Select(m => $"[{ParseRole(m.Content)}] {CompactMessage(StripRolePrefix(m.Content), chatPolicy.ArchiveSummaryLineCharLimit)}")
            .ToList();

        var digest = head;
        if (messagesToArchive.Count > (chatPolicy.ArchiveSummaryTakeHeadLines + chatPolicy.ArchiveSummaryTakeTailLines))
        {
            digest.Add("...");
            digest.AddRange(tail);
        }
        else
        {
            foreach (var line in tail)
            {
                if (!digest.Contains(line))
                {
                    digest.Add(line);
                }
            }
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Archived {messagesToArchive.Count} messages (user: {userCount}, assistant: {assistantCount}).");
        builder.AppendLine("Digest:");

        foreach (var line in digest)
        {
            builder.Append("- ");
            builder.AppendLine(line);
        }

        return builder.ToString().Trim();
    }

    private static int EstimateConversationInputTokens(
        TutorContext context,
        string userMessage,
        List<Message> unsummarizedMessages,
        List<string> archivedSummaries,
        TutorChatPolicy chatPolicy)
    {
        var estimatedSnapshot = new ConversationHistorySnapshot(
            unsummarizedMessages.Count,
            unsummarizedMessages
                .Select(m => CompactMessage(m.Content, chatPolicy.HistoryMessageCharLimit))
                .ToList(),
            BuildOlderHistorySummary(new List<string>(), archivedSummaries, 0, chatPolicy.ArchivedSummaryPromptCharLimit, chatPolicy),
            0);

        var prompt = BuildTutorPrompt(context, userMessage, estimatedSnapshot, chatPolicy);
        return EstimateTokenCount(prompt);
    }

    private static int EstimateTokenCount(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var charEstimate = (int)Math.Ceiling(text.Length / 4.0);
        var wordCount = text
            .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Length;
        var wordEstimate = (int)Math.Ceiling(wordCount * 1.35);

        return Math.Max(charEstimate, wordEstimate);
    }

    private static double CalculateContextUsagePercent(int estimatedPromptTokens, int contextWindow)
    {
        if (contextWindow <= 0)
        {
            return 0d;
        }

        var usagePercent = (estimatedPromptTokens / (double)contextWindow) * 100d;
        return Math.Round(Math.Max(usagePercent, 0d), 2);
    }

    private static ModelTokenBudget ResolveModelTokenBudget(AIProviderConfig config, TutorChatPolicy chatPolicy)
    {
        var (modelName, capabilityContextWindow) = TutorChatRuntimePolicy.ResolveModelContext(config.ConfigJson, DefaultAssistantModel);
        var runtimeContextWindow = capabilityContextWindow;

        if (chatPolicy.RuntimeContextBudget > 0)
        {
            runtimeContextWindow = Math.Min(runtimeContextWindow, chatPolicy.RuntimeContextBudget);
        }

        runtimeContextWindow = Math.Max(runtimeContextWindow, 512);

        var reservedOutput = Math.Max(
            chatPolicy.ReservedOutputMin,
            Math.Min(
                chatPolicy.ReservedOutputMax,
                (int)Math.Round(runtimeContextWindow * chatPolicy.ReservedOutputRatio)));
        var usableWindow = Math.Max(runtimeContextWindow - reservedOutput, 512);
        var summaryTriggerBudget = Math.Clamp(
            (int)Math.Round(usableWindow * chatPolicy.SummaryTriggerRatio),
            256,
            usableWindow);
        var forceSummaryMin = Math.Min(usableWindow, summaryTriggerBudget + 128);
        var forceSummaryBudget = Math.Clamp(
            (int)Math.Round(usableWindow * chatPolicy.ForceSummaryRatio),
            forceSummaryMin,
            usableWindow);
        var promptInputBudget = forceSummaryBudget;

        return new ModelTokenBudget(
            modelName,
            runtimeContextWindow,
            reservedOutput,
            summaryTriggerBudget,
            forceSummaryBudget,
            promptInputBudget);
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

    private static string ParseRole(string content)
    {
        if (content.StartsWith("USER:", StringComparison.OrdinalIgnoreCase))
            return "user";

        if (content.StartsWith("ASSISTANT:", StringComparison.OrdinalIgnoreCase))
            return "assistant";

        return "unknown";
    }

    private static string StripRolePrefix(string content)
    {
        if (content.StartsWith("USER:", StringComparison.OrdinalIgnoreCase))
            return content.Substring("USER:".Length).Trim();

        if (content.StartsWith("ASSISTANT:", StringComparison.OrdinalIgnoreCase))
            return content.Substring("ASSISTANT:".Length).Trim();

        return content.Trim();
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
        List<string> LearningPathChapterTitles,
        List<string> ChapterLessonTitles,
        Guid? LessonId,
        string? LessonTitle,
        string? LessonContent
    );

    private sealed record ConversationHistorySnapshot(
        int TotalMessages,
        List<string> RecentMessages,
        string? OlderSummary,
        int EstimatedPromptTokens);

    private sealed record ModelTokenBudget(
        string ModelName,
        int ContextWindow,
        int ReservedOutput,
        int SummaryTriggerBudget,
        int ForceSummaryBudget,
        int PromptInputBudget);

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

