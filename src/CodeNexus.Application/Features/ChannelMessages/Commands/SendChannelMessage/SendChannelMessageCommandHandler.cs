using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.ChannelMessages.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.ChannelMessages.Commands.SendChannelMessage;

public class SendChannelMessageCommandHandler : IRequestHandler<SendChannelMessageCommand, Result<ChannelMessageDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SendChannelMessageCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<ChannelMessageDto>> Handle(SendChannelMessageCommand request, CancellationToken cancellationToken)
    {
        Guid currentUserId;
        try
        {
            currentUserId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<ChannelMessageDto>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var conversation = await GetOrCreateChannelConversation(request.Category, cancellationToken);

        var sender = await _context.Users
            .AsNoTracking()
            .Where(u => u.UserId == currentUserId)
            .Select(u => new { u.UserId, u.FirstName, u.LastName, u.Username })
            .FirstOrDefaultAsync(cancellationToken);

        if (sender == null)
        {
            return Result<ChannelMessageDto>.Failure("USER_NOT_FOUND", "User not found.");
        }

        DirectMessage? repliedMessage = null;
        if (request.ReplyToMessageId.HasValue)
        {
            repliedMessage = await _context.DirectMessages
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MessageId == request.ReplyToMessageId.Value, cancellationToken);

            if (repliedMessage == null || repliedMessage.ConversationId != conversation.ConversationId)
            {
                return Result<ChannelMessageDto>.Failure("MESSAGE_NOT_FOUND", "Reply target message not found.");
            }
        }

        var message = new DirectMessage
        {
            MessageId = NewId.NextGuid(),
            ConversationId = conversation.ConversationId,
            SenderId = currentUserId,
            Content = request.Content.Trim(),
            MessageType = request.MessageType,
            ReplyToMessageId = request.ReplyToMessageId,
            SentAt = DateTime.UtcNow
        };

        conversation.LastMessagePreview = message.Content.Length > 120
            ? message.Content[..120]
            : message.Content;
        conversation.LastMessageAt = message.SentAt;

        _context.DirectMessages.Add(message);
        await _context.SaveChangesAsync(cancellationToken);

        var senderName = string.IsNullOrWhiteSpace($"{sender.FirstName} {sender.LastName}".Trim())
            ? sender.Username
            : $"{sender.FirstName} {sender.LastName}".Trim();

        return Result<ChannelMessageDto>.Success(new ChannelMessageDto(
            message.MessageId,
            message.ConversationId,
            request.Category,
            message.SenderId,
            senderName,
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

    private async Task<DirectConversation> GetOrCreateChannelConversation(SubjectCategory category, CancellationToken cancellationToken)
    {
        var existing = await _context.DirectConversations
            .FirstOrDefaultAsync(c => c.ConversationType == ChatConversationType.Channel && c.Category == category, cancellationToken);

        if (existing != null)
        {
            return existing;
        }

        var conversation = new DirectConversation
        {
            ConversationId = NewId.NextGuid(),
            Category = category,
            ConversationType = ChatConversationType.Channel,
            CreatedAt = DateTime.UtcNow
        };

        _context.DirectConversations.Add(conversation);
        return conversation;
    }
}
