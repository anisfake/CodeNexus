using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.MentorValidationRequests.Commands.RespondToValidationRequest;

public class RespondToValidationRequestCommandHandler : IRequestHandler<RespondToValidationRequestCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public RespondToValidationRequestCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(RespondToValidationRequestCommand request, CancellationToken cancellationToken)
    {
        var mentorId = _currentUserService.GetUserId();

        var entity = await _context.LearningPathValidationRequests
            .FirstOrDefaultAsync(r => r.ValidationRequestId == request.ValidationRequestId, cancellationToken);

        if (entity == null)
            return Result.Failure("VALIDATION_REQUEST_NOT_FOUND", "Validation request not found.");

        if (entity.MentorId != mentorId)
            return Result.Failure("UNAUTHORIZED", "You are not assigned to this validation request.");

        if (entity.Status != ValidationRequestStatus.Pending && entity.Status != ValidationRequestStatus.InReview)
            return Result.Failure("INVALID_STATUS", "This request has already been resolved and cannot be updated.");

        entity.MentorFeedback = string.IsNullOrWhiteSpace(request.Feedback) ? null : request.Feedback.Trim();
        entity.Status = request.Accept ? ValidationRequestStatus.Completed : ValidationRequestStatus.Rejected;
        entity.RespondedAt = DateTime.UtcNow;
        entity.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
