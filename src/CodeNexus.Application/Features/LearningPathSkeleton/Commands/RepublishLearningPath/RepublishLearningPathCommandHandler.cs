using CodeNexus.Application.Common.Events;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Commands.RepublishLearningPath;

public class RepublishLearningPathCommandHandler : IRequestHandler<RepublishLearningPathCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPublisher _publisher;

    public RepublishLearningPathCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IPublisher publisher)
    {
        _context = context;
        _currentUserService = currentUserService;
        _publisher = publisher;
    }

    public async Task<Result> Handle(RepublishLearningPathCommand request, CancellationToken cancellationToken)
    {
        Guid mentorId;
        try
        {
            mentorId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result.Failure("UNAUTHORIZED", "User not authenticated.");
        }

        var mentor = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == mentorId, cancellationToken);

        if (mentor == null)
        {
            return Result.Failure("USER_NOT_FOUND", "User not found.");
        }

        if (!string.Equals(mentor.Role?.RoleName, "Mentor", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure("ACCESS_DENIED", "Access denied.");
        }

        var learningPath = await _context.LearningPaths
            .FirstOrDefaultAsync(lp => lp.PathId == request.PathId, cancellationToken);

        if (learningPath == null)
        {
            return Result.Failure("LEARNING_PATH_NOT_FOUND", "Learning path not found.");
        }

        if (learningPath.UserId != mentorId)
        {
            return Result.Failure("ACCESS_DENIED", "Access denied.");
        }

        if (!string.Equals(learningPath.Status, LearningPathStatus.Draft.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure("PATH_NOT_IN_DRAFT_STATUS", "Learning path must be in Draft status to republish.");
        }

        var requestedVersion = CalculateRequestedVersion(learningPath.VersionNumber, request.IncreaseVersion, request.VersionUpdateType);
        learningPath.VersionNumber = requestedVersion;
        learningPath.Title = BuildVersionedTitle(learningPath.Title, requestedVersion);
        learningPath.Status = LearningPathStatus.Published.ToString();

        await _context.SaveChangesAsync(cancellationToken);

        if (request.IncreaseVersion)
        {
            await _publisher.Publish(
                new LearningPathDraftVersionUpdatedEvent(
                    learningPath.PathId,
                    mentor.UserId,
                    mentor.Username,
                    requestedVersion,
                    DateTime.UtcNow),
                cancellationToken);
        }

        return Result.Success();
    }

    private static decimal CalculateRequestedVersion(decimal currentVersion, bool increaseVersion, DraftVersionUpdateType? versionUpdateType)
    {
        if (!increaseVersion)
        {
            return currentVersion;
        }

        var nextVersion = versionUpdateType switch
        {
            DraftVersionUpdateType.Major => Math.Floor(currentVersion) + 1.0m,
            _ => currentVersion + 0.1m
        };

        return Math.Round(nextVersion, 1, MidpointRounding.AwayFromZero);
    }

    private static string FormatVersionLabel(decimal versionNumber)
    {
        return versionNumber.ToString("0.0", CultureInfo.InvariantCulture);
    }

    private static string NormalizeBaseTitle(string? rawTitle)
    {
        var baseTitle = string.IsNullOrWhiteSpace(rawTitle)
            ? "Learning Path"
            : rawTitle.Trim();

        baseTitle = Regex.Replace(baseTitle, @"\s*-\s*ver\s+\d+(\.\d+)?\s*$", string.Empty, RegexOptions.IgnoreCase).Trim();
        baseTitle = Regex.Replace(baseTitle, @"\s+v\d+(\.\d+)?\s*$", string.Empty, RegexOptions.IgnoreCase).Trim();

        return baseTitle;
    }

    private static string BuildVersionedTitle(string rawTitle, decimal versionNumber)
    {
        return $"{NormalizeBaseTitle(rawTitle)} - ver {FormatVersionLabel(versionNumber)}";
    }
}
