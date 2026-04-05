using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathShares.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathShares.Queries.GetLearningPathShareUpdateContext;

public class GetLearningPathShareUpdateContextQueryHandler
    : IRequestHandler<GetLearningPathShareUpdateContextQuery, Result<LearningPathShareUpdateContextDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetLearningPathShareUpdateContextQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<LearningPathShareUpdateContextDto>> Handle(GetLearningPathShareUpdateContextQuery request, CancellationToken cancellationToken)
    {
        Guid studentId;
        try
        {
            studentId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<LearningPathShareUpdateContextDto>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var share = await _context.LearningPathShares
            .AsNoTracking()
            .Include(s => s.Mentor)
            .Include(s => s.LearningPath)
            .FirstOrDefaultAsync(s => s.ShareId == request.ShareId && s.StudentId == studentId, cancellationToken);

        if (share == null)
        {
            return Result<LearningPathShareUpdateContextDto>.Failure("SHARE_NOT_FOUND", "Learning path share not found.");
        }

        if (share.Status != LearningPathShareStatus.Accepted)
        {
            return Result<LearningPathShareUpdateContextDto>.Failure("INVALID_SHARE_STATE", "Only accepted shares are supported.");
        }

        var currentVersion = share.SourceVersionAtAccept ?? 1;
        var latestVersion = share.LearningPath.VersionNumber;
        var hasNewVersion = latestVersion > currentVersion
            && (!share.IgnoredSourceVersion.HasValue || share.IgnoredSourceVersion.Value < latestVersion);

        return Result<LearningPathShareUpdateContextDto>.Success(new LearningPathShareUpdateContextDto(
            share.ShareId,
            share.PathId,
            share.LearningPath.Title,
            share.AcceptedPathId,
            share.MentorId,
            share.Mentor.Username,
            currentVersion,
            latestVersion,
            hasNewVersion,
            share.IgnoredSourceVersion,
            share.LastNotifiedSourceVersion
        ));
    }
}
