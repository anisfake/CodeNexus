using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.DirectChats.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.DirectChats.Commands.SendDirectMessage;

public class SendDirectMessageCommandHandler : IRequestHandler<SendDirectMessageCommand, Result<DirectMessageDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SendDirectMessageCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<DirectMessageDto>> Handle(SendDirectMessageCommand request, CancellationToken cancellationToken)
    {
        Guid currentUserId;
        try
        {
            currentUserId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<DirectMessageDto>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var conversation = await _context.DirectConversations
            .FirstOrDefaultAsync(c => c.ConversationId == request.ConversationId, cancellationToken);

        if (conversation == null || conversation.ConversationType != ChatConversationType.Direct)
        {
            return Result<DirectMessageDto>.Failure("CONVERSATION_NOT_FOUND", "Conversation not found.");
        }

        if (conversation.MentorId != currentUserId && conversation.StudentId != currentUserId)
        {
            return Result<DirectMessageDto>.Failure("ACCESS_DENIED", "You do not have access to this conversation.");
        }

        DirectMessage? repliedMessage = null;
        if (request.ReplyToMessageId.HasValue)
        {
            repliedMessage = await _context.DirectMessages
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MessageId == request.ReplyToMessageId.Value, cancellationToken);

            if (repliedMessage == null || repliedMessage.ConversationId != conversation.ConversationId)
            {
                return Result<DirectMessageDto>.Failure("MESSAGE_NOT_FOUND", "Reply target message not found.");
            }
        }

        var message = new DirectMessage
        {
            MessageId = NewId.NextGuid(),
            ConversationId = conversation.ConversationId,
            SenderId = currentUserId,
            ReplyToMessageId = request.ReplyToMessageId,
            Content = request.Content.Trim(),
            MessageType = request.MessageType,
            SentAt = DateTime.UtcNow
        };

        var recipientId = currentUserId == conversation.MentorId
            ? conversation.StudentId
            : conversation.MentorId;

        var receipt = new DirectMessageReceipt
        {
            ReceiptId = NewId.NextGuid(),
            MessageId = message.MessageId,
            UserId = recipientId!.Value
        };

        conversation.LastMessagePreview = message.Content.Length > 120
            ? message.Content[..120]
            : message.Content;
        conversation.LastMessageAt = message.SentAt;

        _context.DirectMessages.Add(message);
        _context.DirectMessageReceipts.Add(receipt);

        await _context.SaveChangesAsync(cancellationToken);

        return Result<DirectMessageDto>.Success(new DirectMessageDto(
            message.MessageId,
            message.ConversationId,
            message.SenderId,
            message.Content,
            message.MessageType,
            message.SentAt,
            null,
            null,
            message.LearningPathShareId,
            message.ReplyToMessageId,
            repliedMessage?.Content,
            repliedMessage?.SenderId
        ));
    }
}
