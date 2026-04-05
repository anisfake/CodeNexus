using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.TutorChat.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace CodeNexus.Application.Features.TutorChat.Commands.SendTutorMessage;

public class SendTutorMessageCommandHandler : IRequestHandler<SendTutorMessageCommand, Result<TutorChatResponseDto>>
{
    private const int RecentHistoryMinMessages = 10;
    private const int RecentHistoryMaxMessages = 20;
    private const int RecentHistoryEmergencyMinMessages = 6;
    private const int SummaryPreserveRecentMessages = RecentHistoryMaxMessages;
    private const double SummaryTriggerRatio = 0.7;
    private const int OlderDigestInitialLineLimit = 12;
    private const int OlderDigestStep = 2;
    private const int ArchivedSummaryPromptTake = 6;
    private const int ArchivedSummaryPromptCharLimit = 260;
    private const int ArchiveSummaryTakeHeadLines = 6;
    private const int ArchiveSummaryTakeTailLines = 6;
    private const int ArchiveSummaryLineCharLimit = 180;
    private const int HistoryMessageCharLimit = 260;
    private const int HistoryMessageMinCharLimit = 90;
    private const int OlderHistoryCharLimit = 180;
    private const int OlderHistoryMinCharLimit = 100;
    private const int LearningPathDescriptionCharLimit = 600;
    private const int ChapterContentCharLimit = 1800;
    private const int LessonContentCharLimit = 2200;
    private const string DefaultAssistantModel = "meta-llama/llama-4-scout-17b-16e-instruct";

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
        var modelTokenBudget = ResolveModelTokenBudget(config);

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

        await EnsureConversationSummaryWithinModelBudgetAsync(
            conversation,
            contextData.Value!,
            normalizedUserMessage,
            modelTokenBudget,
            cancellationToken);

        var historySnapshot = await LoadConversationHistorySnapshotAsync(
            conversation.ConversationId,
            contextData.Value!,
            normalizedUserMessage,
            modelTokenBudget,
            cancellationToken);

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

    private async Task EnsureConversationSummaryWithinModelBudgetAsync(
        Conversation conversation,
        TutorContext context,
        string userMessage,
        ModelTokenBudget tokenBudget,
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

            if (unsummarizedMessages.Count <= SummaryPreserveRecentMessages)
            {
                return;
            }

            var archivedSummaries = await _context.ConversationSummaries
                .AsNoTracking()
                .Where(s => s.ConversationId == conversation.ConversationId)
                .OrderByDescending(s => s.CreatedAt)
                .Take(ArchivedSummaryPromptTake)
                .OrderBy(s => s.CreatedAt)
                .Select(s => s.SummaryContent)
                .ToListAsync(cancellationToken);

            var estimatedInputTokens = EstimateConversationInputTokens(
                context,
                userMessage,
                unsummarizedMessages,
                archivedSummaries);

            if (estimatedInputTokens < tokenBudget.SummaryTriggerBudget)
            {
                return;
            }

            var summarizeCount = unsummarizedMessages.Count - SummaryPreserveRecentMessages;
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
                SummaryContent = BuildArchivedSummaryContent(messagesToSummarize),
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
            .Take(ArchivedSummaryPromptTake)
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
            var onlySummary = BuildOlderHistorySummary(new List<string>(), archivedSummaries, 0, 0);
            return new ConversationHistorySnapshot(totalMessages, new List<string>(), onlySummary);
        }

        var absoluteMinRecent = Math.Min(RecentHistoryMinMessages, activeMessages.Count);
        var emergencyMinRecent = Math.Min(RecentHistoryEmergencyMinMessages, activeMessages.Count);

        var recentCount = Math.Min(RecentHistoryMaxMessages, activeMessages.Count);
        var recentCharLimit = HistoryMessageCharLimit;
        var olderLineLimit = OlderDigestInitialLineLimit;
        var olderCharLimit = OlderHistoryCharLimit;

        var snapshot = BuildSnapshot(
            activeMessages,
            archivedSummaries,
            totalMessages,
            recentCount,
            recentCharLimit,
            olderLineLimit,
            olderCharLimit);

        var estimatedTokens = EstimateTokenCount(BuildTutorPrompt(context, userMessage, snapshot));

        while (estimatedTokens > tokenBudget.SafeInputBudget)
        {
            if (olderLineLimit > 0)
            {
                olderLineLimit = Math.Max(0, olderLineLimit - OlderDigestStep);
            }
            else if (recentCount > absoluteMinRecent)
            {
                recentCount--;
            }
            else if (recentCharLimit > 140)
            {
                recentCharLimit = Math.Max(140, recentCharLimit - 20);
            }
            else if (olderCharLimit > 120)
            {
                olderCharLimit = Math.Max(120, olderCharLimit - 20);
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
                olderCharLimit);

            estimatedTokens = EstimateTokenCount(BuildTutorPrompt(context, userMessage, snapshot));
        }

        while (estimatedTokens > tokenBudget.HardStop)
        {
            if (recentCharLimit > HistoryMessageMinCharLimit)
            {
                recentCharLimit = Math.Max(HistoryMessageMinCharLimit, recentCharLimit - 15);
            }
            else if (olderCharLimit > OlderHistoryMinCharLimit)
            {
                olderCharLimit = Math.Max(OlderHistoryMinCharLimit, olderCharLimit - 10);
            }
            else if (recentCount > emergencyMinRecent)
            {
                recentCount--;
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
                olderCharLimit);

            estimatedTokens = EstimateTokenCount(BuildTutorPrompt(context, userMessage, snapshot));
        }

        return snapshot;
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

        var chapterPathOutline = context.LearningPathChapterTitles.Count == 0
            ? "N/A"
            : string.Join("\n", context.LearningPathChapterTitles.Select(title => $"- {title}"));

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
        int olderCharLimit)
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
            olderCharLimit);

        return new ConversationHistorySnapshot(totalMessages, recentMessages, olderSummary);
    }

    private static string BuildOlderHistorySummary(
        List<string> olderMessages,
        List<string> archivedSummaries,
        int lineLimit,
        int lineCharLimit)
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
                builder.AppendLine(CompactMessage(archived, ArchivedSummaryPromptCharLimit));
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

    private static string BuildArchivedSummaryContent(List<Message> messagesToArchive)
    {
        var userCount = messagesToArchive.Count(m => ParseRole(m.Content) == "user");
        var assistantCount = messagesToArchive.Count(m => ParseRole(m.Content) == "assistant");

        var head = messagesToArchive
            .Take(ArchiveSummaryTakeHeadLines)
            .Select(m => $"[{ParseRole(m.Content)}] {CompactMessage(StripRolePrefix(m.Content), ArchiveSummaryLineCharLimit)}")
            .ToList();

        var tail = messagesToArchive
            .TakeLast(ArchiveSummaryTakeTailLines)
            .Select(m => $"[{ParseRole(m.Content)}] {CompactMessage(StripRolePrefix(m.Content), ArchiveSummaryLineCharLimit)}")
            .ToList();

        var digest = head;
        if (messagesToArchive.Count > (ArchiveSummaryTakeHeadLines + ArchiveSummaryTakeTailLines))
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
        List<string> archivedSummaries)
    {
        var estimatedSnapshot = new ConversationHistorySnapshot(
            unsummarizedMessages.Count,
            unsummarizedMessages
                .Select(m => CompactMessage(m.Content, HistoryMessageCharLimit))
                .ToList(),
            BuildOlderHistorySummary(new List<string>(), archivedSummaries, 0, ArchivedSummaryPromptCharLimit));

        var prompt = BuildTutorPrompt(context, userMessage, estimatedSnapshot);
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

    private static ModelTokenBudget ResolveModelTokenBudget(AIProviderConfig config)
    {
        var (modelName, configuredContextWindow) = ParseModelHints(config.ConfigJson);
        var normalizedModelName = string.IsNullOrWhiteSpace(modelName) ? DefaultAssistantModel : modelName.Trim();
        var contextWindow = configuredContextWindow ?? InferContextWindowFromModel(normalizedModelName);

        if (contextWindow < 4096)
        {
            contextWindow = 4096;
        }

        var reservedOutput = Math.Max(1024, Math.Min(4096, (int)Math.Round(contextWindow * 0.08)));
        var usableWindow = Math.Max(contextWindow - reservedOutput, 2048);
        var summaryTriggerBudget = (int)Math.Round(contextWindow * SummaryTriggerRatio);

        var safeInputBudget = (int)Math.Round(usableWindow * 0.7);
        var hardStop = (int)Math.Round(usableWindow * 0.85);

        if (hardStop <= safeInputBudget)
        {
            hardStop = safeInputBudget + 256;
        }

        return new ModelTokenBudget(
            normalizedModelName,
            contextWindow,
            reservedOutput,
            summaryTriggerBudget,
            safeInputBudget,
            hardStop);
    }

    private static (string? ModelName, int? ContextWindow) ParseModelHints(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            return (null, null);
        }

        try
        {
            using var document = JsonDocument.Parse(configJson);
            var root = document.RootElement;

            string? modelName = null;
            int? contextWindow = null;

            if (TryGetStringPropertyIgnoreCase(root, "model", out var parsedModel))
            {
                modelName = parsedModel;
            }

            if (TryGetIntPropertyIgnoreCase(root, out var parsedContextWindow,
                    "contextWindow",
                    "context_window",
                    "maxContextTokens",
                    "max_context_tokens"))
            {
                contextWindow = parsedContextWindow;
            }

            return (modelName, contextWindow);
        }
        catch
        {
            return (null, null);
        }
    }

    private static bool TryGetStringPropertyIgnoreCase(JsonElement root, string propertyName, out string? value)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            value = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString()
                : property.Value.ToString();
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryGetIntPropertyIgnoreCase(JsonElement root, out int value, params string[] propertyNames)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (!propertyNames.Any(name => string.Equals(name, property.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Number
                && property.Value.TryGetInt32(out value))
            {
                return true;
            }

            if (property.Value.ValueKind == JsonValueKind.String
                && int.TryParse(property.Value.GetString(), out value))
            {
                return true;
            }
        }

        value = 0;
        return false;
    }

    private static int InferContextWindowFromModel(string modelName)
    {
        var normalized = modelName.ToLowerInvariant();

        if (normalized.Contains("1m") || normalized.Contains("1000k"))
            return 1_000_000;

        if (normalized.Contains("256k"))
            return 256_000;

        if (normalized.Contains("200k"))
            return 200_000;

        if (normalized.Contains("128k")
            || normalized.Contains("llama-4")
            || normalized.Contains("llama-3.3")
            || normalized.Contains("llama-3.1")
            || normalized.Contains("qwen2.5")
            || normalized.Contains("gpt-4.1")
            || normalized.Contains("gpt-5"))
            return 128_000;

        if (normalized.Contains("64k"))
            return 64_000;

        if (normalized.Contains("32k")
            || normalized.Contains("mistral")
            || normalized.Contains("mixtral")
            || normalized.Contains("codestral"))
            return 32_000;

        if (normalized.Contains("16k"))
            return 16_000;

        if (normalized.Contains("8k") || normalized.Contains("gemma"))
            return 8_000;

        return 32_000;
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
        string? OlderSummary);

    private sealed record ModelTokenBudget(
        string ModelName,
        int ContextWindow,
        int ReservedOutput,
        int SummaryTriggerBudget,
        int SafeInputBudget,
        int HardStop);

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
