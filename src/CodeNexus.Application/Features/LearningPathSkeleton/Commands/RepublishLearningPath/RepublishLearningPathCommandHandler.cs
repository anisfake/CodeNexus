using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Commands.RepublishLearningPath;

public class RepublishLearningPathCommandHandler : IRequestHandler<RepublishLearningPathCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public RepublishLearningPathCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
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

        learningPath.Status = LearningPathStatus.Published.ToString();

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
