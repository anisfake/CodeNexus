using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathShares.DTOs;
using CodeNexus.Application.Features.LearningPathShares.Services;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathShares.Commands.AcceptLearningPathShare;

public class AcceptLearningPathShareCommandHandler : IRequestHandler<AcceptLearningPathShareCommand, Result<LearningPathShareDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILearningPathSharePathSyncService _pathSyncService;

    public AcceptLearningPathShareCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        ILearningPathSharePathSyncService pathSyncService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _pathSyncService = pathSyncService;
    }

    public async Task<Result<LearningPathShareDto>> Handle(AcceptLearningPathShareCommand request, CancellationToken cancellationToken)
    {
        Guid studentId;
        try
        {
            studentId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<LearningPathShareDto>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var student = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == studentId, cancellationToken);

        if (student == null)
        {
            return Result<LearningPathShareDto>.Failure("USER_NOT_FOUND", "User not found.");
        }

        if (!string.Equals(student.Role?.RoleName, "Student", StringComparison.OrdinalIgnoreCase))
        {
            return Result<LearningPathShareDto>.Failure("ACCESS_DENIED", "Access denied.");
        }

        var share = await _context.LearningPathShares
            .FirstOrDefaultAsync(s => s.ShareId == request.ShareId && s.StudentId == studentId, cancellationToken);

        if (share == null)
        {
            return Result<LearningPathShareDto>.Failure("SHARE_NOT_FOUND", "Learning path share not found.");
        }

        if (share.Status != LearningPathShareStatus.Pending)
        {
            return Result<LearningPathShareDto>.Failure("INVALID_SHARE_STATE", "Only pending shares can be processed.");
        }

        var sourcePath = await _context.LearningPaths
            .AsNoTracking()
            .Include(lp => lp.LearningPathGoals)
            .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.Lessons.Where(l => !l.IsDeleted))
                .ThenInclude(l => l.Quizzes.Where(q => !q.IsDeleted))
            .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.Tasks.Where(t => !t.IsDeleted))
            .FirstOrDefaultAsync(lp => lp.PathId == share.PathId, cancellationToken);

        if (sourcePath == null)
        {
            return Result<LearningPathShareDto>.Failure("LEARNING_PATH_NOT_FOUND", "Learning path not found.");
        }

        var acceptedAt = _dateTimeProvider.UtcNow.AddHours(7);
        var studentPathId = await _pathSyncService.ClonePathForStudentAsync(
            sourcePath,
            studentId,
            acceptedAt,
            cancellationToken);

        share.Status = LearningPathShareStatus.Accepted;
        share.RespondedAt = acceptedAt;
        share.AcceptedPathId = studentPathId;
        share.SourceVersionAtAccept = sourcePath.VersionNumber;
        share.SourceSnapshotJson = LearningPathShareSourceSnapshotHelper.CreateSnapshotJson(sourcePath);
        share.IgnoredSourceVersion = null;
        share.LastNotifiedSourceVersion = null;
        share.IsTrackingEnabled = true;
        share.InvalidatedReason = null;

        await _context.SaveChangesAsync(cancellationToken);

        return Result<LearningPathShareDto>.Success(new LearningPathShareDto(
            share.ShareId,
            share.PathId,
            share.MentorId,
            share.StudentId,
            share.Status,
            share.SentAt,
            share.RespondedAt,
            share.AcceptedPathId,
            share.SourceVersionAtAccept,
            share.IgnoredSourceVersion,
            share.LastNotifiedSourceVersion,
            share.IsTrackingEnabled,
            share.InvalidatedReason
        ));
    }
}
