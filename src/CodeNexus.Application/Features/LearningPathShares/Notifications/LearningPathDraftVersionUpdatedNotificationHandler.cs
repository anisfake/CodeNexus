using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Events;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathShares.Notifications;

public class LearningPathDraftVersionUpdatedInvalidatePendingSharesHandler
    : INotificationHandler<LearningPathDraftVersionUpdatedEvent>
{
    private readonly IApplicationDbContext _context;

    public LearningPathDraftVersionUpdatedInvalidatePendingSharesHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(LearningPathDraftVersionUpdatedEvent notification, CancellationToken cancellationToken)
    {
        var pendingShares = await _context.LearningPathShares
            .Where(s => s.PathId == notification.PathId && s.Status == LearningPathShareStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var pendingShare in pendingShares)
        {
            pendingShare.Status = LearningPathShareStatus.Rejected;
            pendingShare.RespondedAt = notification.OccurredAt;
            pendingShare.InvalidatedReason = "SUPERSEDED_BY_NEW_VERSION";
        }

        if (pendingShares.Count == 0)
        {
            return;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
