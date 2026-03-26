using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.ChannelMessages.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.ChannelMessages.Queries.GetChannelMessages;

public class GetChannelMessagesQueryHandler : IRequestHandler<GetChannelMessagesQuery, Result<PaginationDto<ChannelMessageDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetChannelMessagesQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PaginationDto<ChannelMessageDto>>> Handle(GetChannelMessagesQuery request, CancellationToken cancellationToken)
    {
        Guid currentUserId;
        try
        {
            currentUserId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<PaginationDto<ChannelMessageDto>>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var conversation = await _context.DirectConversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ConversationType == ChatConversationType.Channel && c.Category == request.Category, cancellationToken);

        if (conversation == null)
        {
            return Result<PaginationDto<ChannelMessageDto>>.Success(new PaginationDto<ChannelMessageDto>
            {
                Items = new List<ChannelMessageDto>(),
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                TotalCount = 0
            });
        }

        var query = _context.DirectMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversation.ConversationId);

        var totalCount = await query.CountAsync(cancellationToken);

        var messages = await query
            .OrderByDescending(m => m.SentAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(m => new
            {
                m.MessageId,
                m.ConversationId,
                m.SenderId,
                SenderName = string.IsNullOrWhiteSpace((m.Sender.FirstName ?? string.Empty) + " " + (m.Sender.LastName ?? string.Empty))
                    ? m.Sender.Username
                    : ((m.Sender.FirstName ?? string.Empty) + " " + (m.Sender.LastName ?? string.Empty)).Trim(),
                m.Content,
                m.MessageType,
                m.SentAt,
                m.LearningPathShareId,
                m.ReplyToMessageId
            })
            .OrderBy(m => m.SentAt)
            .ToListAsync(cancellationToken);

        var messageIds = messages.Select(m => m.MessageId).ToList();

        var receipts = await _context.DirectMessageReceipts
            .AsNoTracking()
            .Where(r => messageIds.Contains(r.MessageId) && r.UserId == currentUserId)
            .ToDictionaryAsync(r => r.MessageId, cancellationToken);

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

        var items = messages
            .Select(m =>
            {
                receipts.TryGetValue(m.MessageId, out var receipt);

                (string Content, Guid SenderId)? replyInfo = null;
                if (m.ReplyToMessageId.HasValue && replyToMessageLookup.TryGetValue(m.ReplyToMessageId.Value, out var reply))
                {
                    replyInfo = reply;
                }

                return new ChannelMessageDto(
                    m.MessageId,
                    m.ConversationId,
                    request.Category,
                    m.SenderId,
                    m.SenderName,
                    m.Content,
                    m.MessageType,
                    m.SentAt,
                    receipt?.DeliveredAt,
                    receipt?.SeenAt,
                    m.LearningPathShareId,
                    m.ReplyToMessageId,
                    replyInfo?.Content,
                    replyInfo?.SenderId);
            })
            .ToList();

        return Result<PaginationDto<ChannelMessageDto>>.Success(new PaginationDto<ChannelMessageDto>
        {
            Items = items,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalCount = totalCount
        });
    }
}
