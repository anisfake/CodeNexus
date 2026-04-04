using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.ChannelMessages.Commands.MarkChannelMessageDelivered;

public class MarkChannelMessageDeliveredCommandHandler : IRequestHandler<MarkChannelMessageDeliveredCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public MarkChannelMessageDeliveredCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(MarkChannelMessageDeliveredCommand request, CancellationToken cancellationToken)
    {
        Guid currentUserId;
        try
        {
            currentUserId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var message = await _context.DirectMessages
            .Include(m => m.Conversation)
            .FirstOrDefaultAsync(m => m.MessageId == request.MessageId, cancellationToken);

        if (message == null)
            return Result.Failure("MESSAGE_NOT_FOUND", "Message not found.");

        if (message.Conversation.ConversationType != ChatConversationType.Channel)
            return Result.Failure("ACCESS_DENIED", "Access denied.");

        if (message.SenderId == currentUserId)
            return Result.Failure("INVALID_OPERATION", "Invalid operation.");

        var receipt = await _context.DirectMessageReceipts
            .FirstOrDefaultAsync(r => r.MessageId == request.MessageId && r.UserId == currentUserId, cancellationToken);

        if (receipt == null)
        {
            receipt = new DirectMessageReceipt
            {
                ReceiptId = NewId.NextGuid(),
                MessageId = request.MessageId,
                UserId = currentUserId,
                DeliveredAt = DateTime.UtcNow
            };

            _context.DirectMessageReceipts.Add(receipt);
        }
        else if (!receipt.DeliveredAt.HasValue)
        {
            receipt.DeliveredAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
