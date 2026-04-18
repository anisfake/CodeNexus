using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.TutorChat;
using CodeNexus.Application.Features.TutorChat.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.TutorChat.Queries.GetTutorConversationMessages;

public class GetTutorConversationMessagesQueryHandler
    : IRequestHandler<GetTutorConversationMessagesQuery, Result<TutorMessagesPageDto>>
{
    private const string DefaultAssistantModel = "meta-llama/llama-4-scout-17b-16e-instruct";

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetTutorConversationMessagesQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<TutorMessagesPageDto>> Handle(GetTutorConversationMessagesQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var conversation = await _context.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ConversationId == request.ConversationId && !c.IsDeleted, cancellationToken);

        if (conversation == null)
        {
            return Result<TutorMessagesPageDto>.Failure("CONVERSATION_NOT_FOUND", "Conversation not found.");
        }

        if (conversation.UserId != userId)
        {
            return Result<TutorMessagesPageDto>.Failure("ACCESS_DENIED", "Access denied.");
        }

        var totalCount = await _context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == request.ConversationId)
            .CountAsync(cancellationToken);

        var messages = await _context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == request.ConversationId)
            .OrderByDescending(m => m.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        var items = messages.Select(m =>
        {
            var role = ParseRole(m.Content);
            var content = StripRolePrefix(m.Content);
            return new TutorMessageDto(
                m.MessageId,
                m.ConversationId,
                role,
                content,
                m.CreatedAt
            );
        }).ToList();

        var contextUsagePercent = await ResolveContextUsagePercentAsync(
            request.ConversationId,
            userId,
            conversation.ConfigId,
            cancellationToken);

        return Result<TutorMessagesPageDto>.Success(new TutorMessagesPageDto
        {
            Items = items,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalCount = totalCount,
            ContextUsagePercent = contextUsagePercent
        });
    }

    private async Task<double> ResolveContextUsagePercentAsync(
        Guid conversationId,
        Guid userId,
        Guid configId,
        CancellationToken cancellationToken)
    {
        var latestAssistantMessage = await _context.Messages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.Content.StartsWith("ASSISTANT:"))
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new { m.CreatedAt, m.InputTokens })
            .FirstOrDefaultAsync(cancellationToken);

        if (latestAssistantMessage == null)
        {
            return 0d;
        }

        var config = await _context.AIProviderConfigs
            .AsNoTracking()
            .Where(c => c.ConfigId == configId)
            .Select(c => new { c.ConfigJson })
            .FirstOrDefaultAsync(cancellationToken);

        var fallbackModel = DefaultAssistantModel;
        var chatPolicy = TutorChatRuntimePolicy.Resolve(config?.ConfigJson);
        var (resolvedModel, contextWindow) = TutorChatRuntimePolicy.ResolveModelContext(config?.ConfigJson, fallbackModel);

        if (chatPolicy.RuntimeContextBudget > 0)
        {
            contextWindow = Math.Min(contextWindow, chatPolicy.RuntimeContextBudget);
        }

        contextWindow = Math.Max(contextWindow, 512);

        if (contextWindow <= 0)
        {
            return 0d;
        }

        if (latestAssistantMessage.InputTokens.HasValue && latestAssistantMessage.InputTokens.Value > 0)
        {
            var exactUsagePercent = (latestAssistantMessage.InputTokens.Value / (double)contextWindow) * 100d;
            return Math.Round(Math.Clamp(exactUsagePercent, 0d, 100d), 2);
        }

        var latestAssistantAt = latestAssistantMessage.CreatedAt;
        var logWindowStart = latestAssistantAt.AddMinutes(-5);
        var logWindowEnd = latestAssistantAt.AddSeconds(30);

        var usageLogsInWindow = _context.AIUsageLogs
            .AsNoTracking()
            .Where(log =>
                log.UserId == userId
                && log.UsageType == AIUsageType.Assistant
                && log.CreatedAt >= logWindowStart
                && log.CreatedAt <= logWindowEnd);

        var usageLog = string.IsNullOrWhiteSpace(resolvedModel)
            ? null
            : await usageLogsInWindow
                .Where(log => log.Model == resolvedModel)
                .OrderByDescending(log => log.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

        usageLog ??= await usageLogsInWindow
            .OrderByDescending(log => log.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        usageLog ??= await _context.AIUsageLogs
            .AsNoTracking()
            .Where(log =>
                log.UserId == userId
                && log.UsageType == AIUsageType.Assistant
                && log.CreatedAt <= latestAssistantAt.AddSeconds(30))
            .OrderByDescending(log => log.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (usageLog == null)
        {
            return 0d;
        }

        var usagePercent = (usageLog.InputTokens / (double)contextWindow) * 100d;
        return Math.Round(Math.Clamp(usagePercent, 0d, 100d), 2);
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
}
