using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathShares.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathShares.Commands.SendLearningPathShare;

public class SendLearningPathShareCommandHandler : IRequestHandler<SendLearningPathShareCommand, Result<LearningPathShareDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SendLearningPathShareCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<LearningPathShareDto>> Handle(SendLearningPathShareCommand request, CancellationToken cancellationToken)
    {
        Guid mentorId;
        try
        {
            mentorId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<LearningPathShareDto>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var mentor = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == mentorId, cancellationToken);

        if (mentor == null)
        {
            return Result<LearningPathShareDto>.Failure("USER_NOT_FOUND", "User not found.");
        }

        if (!string.Equals(mentor.Role?.RoleName, "Mentor", StringComparison.OrdinalIgnoreCase))
        {
            return Result<LearningPathShareDto>.Failure("ACCESS_DENIED", "Only mentors can share learning paths.");
        }

        var student = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == request.StudentId, cancellationToken);

        if (student == null)
        {
            return Result<LearningPathShareDto>.Failure("STUDENT_NOT_FOUND", "Student not found.");
        }

        if (!string.Equals(student.Role?.RoleName, "Student", StringComparison.OrdinalIgnoreCase))
        {
            return Result<LearningPathShareDto>.Failure("INVALID_RECIPIENT", "Recipient must be a student.");
        }

        var path = await _context.LearningPaths
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PathId == request.PathId, cancellationToken);

        if (path == null)
        {
            return Result<LearningPathShareDto>.Failure("LEARNING_PATH_NOT_FOUND", "Learning path not found.");
        }

        if (path.UserId != mentorId)
        {
            return Result<LearningPathShareDto>.Failure("ACCESS_DENIED", "You can only share your own learning path.");
        }

        var existingPendingShare = await _context.LearningPathShares
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.PathId == request.PathId
                                   && s.MentorId == mentorId
                                   && s.StudentId == request.StudentId
                                   && s.Status == LearningPathShareStatus.Pending,
                cancellationToken);

        if (existingPendingShare != null)
        {
            return Result<LearningPathShareDto>.Failure("SHARE_ALREADY_PENDING", "A pending share already exists for this student.");
        }

        var share = new LearningPathShare
        {
            ShareId = NewId.NextGuid(),
            PathId = request.PathId,
            MentorId = mentorId,
            StudentId = request.StudentId,
            Status = LearningPathShareStatus.Pending,
            SentAt = DateTime.UtcNow
        };

        _context.LearningPathShares.Add(share);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<LearningPathShareDto>.Success(new LearningPathShareDto(
            share.ShareId,
            share.PathId,
            share.MentorId,
            share.StudentId,
            share.Status,
            share.SentAt,
            share.RespondedAt
        ));
    }
}
