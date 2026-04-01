using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.DirectChats.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.DirectChats.Queries.GetConversationMessages;

public class GetConversationMessagesQueryHandler : IRequestHandler<GetConversationMessagesQuery, Result<PaginationDto<DirectMessageDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetConversationMessagesQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PaginationDto<DirectMessageDto>>> Handle(GetConversationMessagesQuery request, CancellationToken cancellationToken)
    {
        Guid currentUserId;
        try
        {
            currentUserId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<PaginationDto<DirectMessageDto>>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var conversation = await _context.DirectConversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ConversationId == request.ConversationId, cancellationToken);

        if (conversation == null || conversation.ConversationType != ChatConversationType.Direct)
        {
            return Result<PaginationDto<DirectMessageDto>>.Failure("CONVERSATION_NOT_FOUND", "Conversation not found.");
        }

        if (conversation.MentorId != currentUserId && conversation.StudentId != currentUserId)
        {
            return Result<PaginationDto<DirectMessageDto>>.Failure("ACCESS_DENIED", "Access denied.");
        }

        var totalCount = await _context.DirectMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == request.ConversationId)
            .CountAsync(cancellationToken);

        var messages = await _context.DirectMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == request.ConversationId)
            .OrderByDescending(m => m.SentAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .OrderBy(m => m.SentAt)
            .ToListAsync(cancellationToken);

        var messageIds = messages.Select(m => m.MessageId).ToList();

        var receipts = await _context.DirectMessageReceipts
            .AsNoTracking()
            .Where(r => messageIds.Contains(r.MessageId))
            .ToListAsync(cancellationToken);

        var replyToMessageIds = messages
            .Where(m => m.ReplyToMessageId.HasValue)
            .Select(m => m.ReplyToMessageId!.Value)
            .Distinct()
            .ToList();

        var replyToMessageLookup = replyToMessageIds.Count == 0
            ? new Dictionary<Guid, (string Content, Guid SenderId)>()
            : await _context.DirectMessages
                .AsNoTracking()
                .Where(m => replyToMessageIds.Contains(m.MessageId))
                .Select(m => new { m.MessageId, m.Content, m.SenderId })
                .ToDictionaryAsync(m => m.MessageId, m => (m.Content, m.SenderId), cancellationToken);

        var items = messages.Select(m =>
        {
            var recipientId = m.SenderId == conversation.MentorId
                ? conversation.StudentId
                : conversation.MentorId;

            var receipt = receipts.FirstOrDefault(r => r.MessageId == m.MessageId && r.UserId == recipientId);

            var replyInfo = m.ReplyToMessageId.HasValue && replyToMessageLookup.TryGetValue(m.ReplyToMessageId.Value, out var value)
                ? value
                : ((string Content, Guid SenderId)?)null;

            return new DirectMessageDto(
                m.MessageId,
                m.ConversationId,
                m.SenderId,
                m.Content,
                m.MessageType,
                m.SentAt,
                receipt?.DeliveredAt,
                receipt?.SeenAt,
                m.LearningPathShareId,
                m.ReplyToMessageId,
                replyInfo?.Content,
                replyInfo?.SenderId
            );
        }).ToList();

        return Result<PaginationDto<DirectMessageDto>>.Success(new PaginationDto<DirectMessageDto>
        {
            Items = items,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalCount = totalCount
        });
    }
}
