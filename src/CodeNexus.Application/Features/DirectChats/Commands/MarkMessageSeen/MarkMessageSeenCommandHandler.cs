using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Domain.Entities;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.DirectChats.Commands.MarkMessageSeen;

public class MarkMessageSeenCommandHandler : IRequestHandler<MarkMessageSeenCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public MarkMessageSeenCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(MarkMessageSeenCommand request, CancellationToken cancellationToken)
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
        {
            return Result.Failure("MESSAGE_NOT_FOUND", "Message not found.");
        }

        var isParticipant = message.Conversation.MentorId == currentUserId ||
                            message.Conversation.StudentId == currentUserId;

        if (!isParticipant)
        {
            return Result.Failure("ACCESS_DENIED", "You do not have access to this conversation.");
        }

        if (message.SenderId == currentUserId)
        {
            return Result.Failure("INVALID_OPERATION", "Sender cannot mark own message as seen.");
        }

        var now = DateTime.UtcNow;

        var receipt = await _context.DirectMessageReceipts
            .FirstOrDefaultAsync(r => r.MessageId == request.MessageId && r.UserId == currentUserId, cancellationToken);

        if (receipt == null)
        {
            receipt = new DirectMessageReceipt
            {
                ReceiptId = NewId.NextGuid(),
                MessageId = request.MessageId,
                UserId = currentUserId,
                DeliveredAt = now,
                SeenAt = now
            };

            _context.DirectMessageReceipts.Add(receipt);
        }
        else
        {
            receipt.DeliveredAt ??= now;
            receipt.SeenAt ??= now;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
