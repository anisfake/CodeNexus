using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.MentorValidationRequests.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.MentorValidationRequests.Commands.SubmitValidationRequest;

public class SubmitValidationRequestCommandHandler : IRequestHandler<SubmitValidationRequestCommand, Result<ValidationRequestDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SubmitValidationRequestCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<ValidationRequestDto>> Handle(SubmitValidationRequestCommand request, CancellationToken cancellationToken)
    {
        var studentId = _currentUserService.GetUserId();

        var student = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == studentId, cancellationToken);

        if (student == null)
            return Result<ValidationRequestDto>.Failure("UNAUTHORIZED", "User not found.");

        var path = await _context.LearningPaths
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PathId == request.PathId, cancellationToken);

        if (path == null)
            return Result<ValidationRequestDto>.Failure("LEARNING_PATH_NOT_FOUND", "Learning path not found.");

        if (path.UserId != studentId)
            return Result<ValidationRequestDto>.Failure("UNAUTHORIZED", "You do not own this learning path.");

        var mentor = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == request.MentorId, cancellationToken);

        if (mentor == null || !string.Equals(mentor.Role?.RoleName, "Mentor", StringComparison.OrdinalIgnoreCase))
            return Result<ValidationRequestDto>.Failure("MENTOR_NOT_FOUND", "Mentor not found.");

        // Quota check
        var subscription = await _context.StudentMentorSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == studentId && s.IsActive, cancellationToken);

        if (subscription == null)
            return Result<ValidationRequestDto>.Failure("MENTOR_SUBSCRIPTION_REQUIRED", "An active mentor subscription is required.");

        if (subscription.ValidationRequestLimit != -1 && subscription.ValidationRequestsUsed >= subscription.ValidationRequestLimit)
            return Result<ValidationRequestDto>.Failure("VALIDATION_QUOTA_EXCEEDED", "You have reached your validation request limit for this subscription.");

        // Duplicate check: no Pending or InReview request for the same path+mentor
        var alreadyPending = await _context.LearningPathValidationRequests
            .AsNoTracking()
            .AnyAsync(r => r.PathId == request.PathId
                        && r.MentorId == request.MentorId
                        && (r.Status == ValidationRequestStatus.Pending || r.Status == ValidationRequestStatus.InReview),
                cancellationToken);

        if (alreadyPending)
            return Result<ValidationRequestDto>.Failure("VALIDATION_REQUEST_ALREADY_PENDING", "A pending or in-review validation request already exists for this path and mentor.");

        var entity = new LearningPathValidationRequest
        {
            ValidationRequestId = NewId.NextGuid(),
            PathId = request.PathId,
            StudentId = studentId,
            MentorId = request.MentorId,
            StudentNote = string.IsNullOrWhiteSpace(request.StudentNote) ? null : request.StudentNote.Trim(),
            Status = ValidationRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        subscription.ValidationRequestsUsed++;
        _context.FeatureUsageLogs.Add(new FeatureUsageLog
        {
            FeatureUsageLogId = NewId.NextGuid(),
            UserId = studentId,
            FeatureKey = SubscriptionFeatureKey.MentorValidationRequest,
            CreatedAt = DateTime.UtcNow
        });

        await _context.LearningPathValidationRequests.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<ValidationRequestDto>.Success(new ValidationRequestDto(
            entity.ValidationRequestId,
            entity.PathId,
            path.Title,
            entity.StudentId,
            student.Username,
            entity.MentorId,
            mentor.Username,
            entity.StudentNote,
            entity.MentorFeedback,
            entity.Status,
            entity.CreatedAt,
            entity.RespondedAt));
    }
}
