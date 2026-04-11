using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathShares.DTOs;
using CodeNexus.Application.Features.LearningPathShares.Services;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathShares.Commands.ApplyLearningPathShareUpdate;

public class ApplyLearningPathShareUpdateCommandHandler : IRequestHandler<ApplyLearningPathShareUpdateCommand, Result<LearningPathShareDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILearningPathSharePathSyncService _pathSyncService;

    public ApplyLearningPathShareUpdateCommandHandler(
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

    public async Task<Result<LearningPathShareDto>> Handle(ApplyLearningPathShareUpdateCommand request, CancellationToken cancellationToken)
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

        var share = await _context.LearningPathShares
            .FirstOrDefaultAsync(s => s.ShareId == request.ShareId && s.StudentId == studentId, cancellationToken);

        if (share == null)
        {
            return Result<LearningPathShareDto>.Failure("SHARE_NOT_FOUND", "Learning path share not found.");
        }

        if (share.Status != LearningPathShareStatus.Accepted)
        {
            return Result<LearningPathShareDto>.Failure("INVALID_SHARE_STATE", "Only accepted shares can be updated.");
        }

        var sourcePath = await _context.LearningPaths
            .AsNoTracking()
            .Include(lp => lp.LearningPathGoals)
            .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.Lessons.Where(l => !l.IsDeleted))
                .ThenInclude(l => l.Quizzes.Where(q => !q.IsDeleted))
            .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                .ThenInclude(c => c.Tasks)
            .FirstOrDefaultAsync(lp => lp.PathId == share.PathId, cancellationToken);

        if (sourcePath == null)
        {
            return Result<LearningPathShareDto>.Failure("SOURCE_LEARNING_PATH_NOT_FOUND", "Source learning path not found.");
        }

        var latestVersion = sourcePath.VersionNumber;
        var currentSourceVersion = share.SourceVersionAtAccept ?? 1.0m;
        var hasNewVersion = latestVersion > currentSourceVersion;

        if (!hasNewVersion)
        {
            return Result<LearningPathShareDto>.Failure("NO_NEW_VERSION_AVAILABLE", "No new version is available.");
        }

        switch (request.Action)
        {
            case LearningPathShareUpdateAction.CreateNewFromLatest:
            {
                var now = _dateTimeProvider.UtcNow.AddHours(7);
                var newPathId = await _pathSyncService.ClonePathForStudentAsync(sourcePath, studentId, now, cancellationToken);
                share.AcceptedPathId = newPathId;
                share.SourceVersionAtAccept = latestVersion;
                share.IgnoredSourceVersion = null;
                share.LastNotifiedSourceVersion = latestVersion;
                share.IsTrackingEnabled = true;
                break;
            }
            case LearningPathShareUpdateAction.UpdateCurrentToLatest:
            {
                if (!share.AcceptedPathId.HasValue)
                {
                    return Result<LearningPathShareDto>.Failure("LEARNING_PATH_NOT_FOUND", "Accepted learning path not found.");
                }

                var acceptedPath = await _context.LearningPaths
                    .Include(lp => lp.LearningPathGoals)
                    .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                        .ThenInclude(c => c.Lessons.Where(l => !l.IsDeleted))
                    .Include(lp => lp.Chapters.Where(c => !c.IsDeleted))
                        .ThenInclude(c => c.Tasks)
                    .FirstOrDefaultAsync(lp => lp.PathId == share.AcceptedPathId.Value && lp.UserId == studentId, cancellationToken);

                if (acceptedPath == null)
                {
                    return Result<LearningPathShareDto>.Failure("LEARNING_PATH_NOT_FOUND", "Accepted learning path not found.");
                }

                var now = _dateTimeProvider.UtcNow.AddHours(7);
                await _pathSyncService.RebuildCurrentPathFromSourceAsync(acceptedPath, sourcePath, studentId, now, cancellationToken);
                share.SourceVersionAtAccept = latestVersion;
                share.IgnoredSourceVersion = null;
                share.LastNotifiedSourceVersion = latestVersion;
                share.IsTrackingEnabled = true;
                break;
            }
            case LearningPathShareUpdateAction.DisableUpdateNotifications:
            {
                share.IgnoredSourceVersion = latestVersion;
                share.LastNotifiedSourceVersion = latestVersion;
                share.IsTrackingEnabled = false;
                break;
            }
            default:
                return Result<LearningPathShareDto>.Failure("INVALID_ACTION", "Invalid update action.");
        }

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
