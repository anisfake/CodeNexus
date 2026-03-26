using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.ChannelMessages.Commands.MarkChannelMessageSeen;

public class MarkChannelMessageSeenCommandHandler : IRequestHandler<MarkChannelMessageSeenCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public MarkChannelMessageSeenCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(MarkChannelMessageSeenCommand request, CancellationToken cancellationToken)
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

        var conversation = message.Conversation;
        if (conversation.ConversationType != ChatConversationType.Channel || !conversation.SubjectId.HasValue)
            return Result.Failure("ACCESS_DENIED", "You do not have access to this channel.");

        var accessResult = await EnsureSubjectAccess(conversation.SubjectId.Value, currentUserId, cancellationToken);
        if (accessResult.IsFailure)
            return accessResult;

        if (message.SenderId == currentUserId)
            return Result.Failure("INVALID_OPERATION", "Sender cannot mark own message as seen.");

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

    private async Task<Result> EnsureSubjectAccess(Guid subjectId, Guid currentUserId, CancellationToken cancellationToken)
    {
        var subject = await _context.Subjects
            .AsNoTracking()
            .Where(s => s.SubjectId == subjectId && !s.IsDeleted)
            .Select(s => new { s.CreatedByUserId })
            .FirstOrDefaultAsync(cancellationToken);

        if (subject == null)
            return Result.Failure("SUBJECT_NOT_FOUND", "Subject not found.");

        var hasLearningPath = await _context.LearningPaths
            .AsNoTracking()
            .AnyAsync(lp => lp.SubjectId == subjectId && lp.UserId == currentUserId, cancellationToken);

        if (subject.CreatedByUserId != currentUserId && !hasLearningPath)
            return Result.Failure("ACCESS_DENIED", "You do not have access to this subject.");

        return Result.Success();
    }
}
