using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Application.Features.LearningPathShares.Services;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathShares.Commands.EnrollInPublishedLearningPath;

public class EnrollInPublishedLearningPathCommandHandler : IRequestHandler<EnrollInPublishedLearningPathCommand, Result<EnrollmentResponseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILearningPathSharePathSyncService _pathSyncService;

    public EnrollInPublishedLearningPathCommandHandler(
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

    public async Task<Result<EnrollmentResponseDto>> Handle(EnrollInPublishedLearningPathCommand request, CancellationToken cancellationToken)
    {
        Guid studentId;
        try
        {
            studentId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<EnrollmentResponseDto>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var student = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == studentId, cancellationToken);

        if (student == null)
        {
            return Result<EnrollmentResponseDto>.Failure("USER_NOT_FOUND", "User not found.");
        }

        if (!string.Equals(student.Role?.RoleName, "Student", StringComparison.OrdinalIgnoreCase))
        {
            return Result<EnrollmentResponseDto>.Failure("ACCESS_DENIED", "Access denied.");
        }

        var sourcePath = await _context.LearningPaths
            .AsNoTracking()
            .Include(lp => lp.LearningPathGoals)
            .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.Lessons.Where(l => !l.IsDeleted))
                .ThenInclude(l => l.Quizzes.Where(q => !q.IsDeleted))
            .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.Tasks.Where(t => !t.IsDeleted))
            .FirstOrDefaultAsync(lp => lp.PathId == request.PathId, cancellationToken);

        if (sourcePath == null)
        {
            return Result<EnrollmentResponseDto>.Failure("LEARNING_PATH_NOT_FOUND", "Learning path not found.");
        }

        if (!string.Equals(sourcePath.Status, LearningPathStatus.Published.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return Result<EnrollmentResponseDto>.Failure("LEARNING_PATH_NOT_PUBLISHED", "Learning path is not published.");
        }

        var alreadyEnrolled = await _context.LearningPathShares
            .AsNoTracking()
            .AnyAsync(s => s.PathId == request.PathId
                           && s.StudentId == studentId
                           && s.Status == LearningPathShareStatus.Accepted,
                cancellationToken);

        if (alreadyEnrolled)
        {
            return Result<EnrollmentResponseDto>.Failure("ALREADY_ENROLLED", "You are already enrolled in this learning path.");
        }

        var enrolledAt = _dateTimeProvider.UtcNow.AddHours(7);
        var enrolledPathId = await _pathSyncService.ClonePathForStudentAsync(
            sourcePath,
            studentId,
            enrolledAt,
            cancellationToken);

        var share = new LearningPathShare
        {
            ShareId = NewId.NextGuid(),
            PathId = sourcePath.PathId,
            MentorId = sourcePath.UserId,
            StudentId = studentId,
            SnapshotTitle = sourcePath.Title,
            Status = LearningPathShareStatus.Accepted,
            AcceptedPathId = enrolledPathId,
            SourceVersionAtAccept = sourcePath.VersionNumber,
            IgnoredSourceVersion = null,
            LastNotifiedSourceVersion = null,
            IsTrackingEnabled = true,
            SentAt = enrolledAt,
            RespondedAt = enrolledAt
        };

        await _context.LearningPathShares.AddAsync(share, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<EnrollmentResponseDto>.Success(new EnrollmentResponseDto(
            share.ShareId,
            enrolledPathId,
            sourcePath.VersionNumber));
    }
}
