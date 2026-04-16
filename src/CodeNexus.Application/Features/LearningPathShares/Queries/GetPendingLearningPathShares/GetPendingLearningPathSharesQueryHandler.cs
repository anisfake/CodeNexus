using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathShares.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathShares.Queries.GetPendingLearningPathShares;

public class GetPendingLearningPathSharesQueryHandler : IRequestHandler<GetPendingLearningPathSharesQuery, Result<List<LearningPathShareSummaryDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetPendingLearningPathSharesQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<List<LearningPathShareSummaryDto>>> Handle(GetPendingLearningPathSharesQuery request, CancellationToken cancellationToken)
    {
        Guid studentId;
        try
        {
            studentId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<List<LearningPathShareSummaryDto>>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var student = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == studentId, cancellationToken);

        if (student == null)
        {
            return Result<List<LearningPathShareSummaryDto>>.Failure("USER_NOT_FOUND", "User not found.");
        }

        if (!string.Equals(student.Role?.RoleName, "Student", StringComparison.OrdinalIgnoreCase))
        {
            return Result<List<LearningPathShareSummaryDto>>.Failure("ACCESS_DENIED", "Access denied.");
        }

        var shares = await _context.LearningPathShares
            .AsNoTracking()
            .Where(s => s.StudentId == studentId && s.Status == LearningPathShareStatus.Pending)
            .Include(s => s.LearningPath)
            .Include(s => s.Mentor)
            .OrderByDescending(s => s.SentAt)
            .Select(s => new LearningPathShareSummaryDto(
                s.ShareId,
                s.PathId,
                s.SnapshotTitle,
                s.LearningPath.Description,
                s.MentorId,
                s.Mentor.Username,
                s.Status,
                s.SentAt
            ))
            .ToListAsync(cancellationToken);

        return Result<List<LearningPathShareSummaryDto>>.Success(shares);
    }
}
