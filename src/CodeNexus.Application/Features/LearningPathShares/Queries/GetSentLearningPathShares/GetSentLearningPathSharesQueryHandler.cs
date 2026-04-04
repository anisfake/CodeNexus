using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathShares.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.LearningPathShares.Queries.GetSentLearningPathShares;

public class GetSentLearningPathSharesQueryHandler : IRequestHandler<GetSentLearningPathSharesQuery, Result<List<SentLearningPathShareSummaryDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetSentLearningPathSharesQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<List<SentLearningPathShareSummaryDto>>> Handle(GetSentLearningPathSharesQuery request, CancellationToken cancellationToken)
    {
        Guid currentUserId;
        try
        {
            currentUserId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<List<SentLearningPathShareSummaryDto>>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var currentUser = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == currentUserId, cancellationToken);

        if (currentUser == null)
        {
            return Result<List<SentLearningPathShareSummaryDto>>.Failure("USER_NOT_FOUND", "User not found.");
        }

        var isMentor = string.Equals(currentUser.Role?.RoleName, "Mentor", StringComparison.OrdinalIgnoreCase);
        var isStudent = string.Equals(currentUser.Role?.RoleName, "Student", StringComparison.OrdinalIgnoreCase);

        if (!isMentor && !isStudent)
        {
            return Result<List<SentLearningPathShareSummaryDto>>.Failure("ACCESS_DENIED", "Access denied.");
        }

        var query = _context.LearningPathShares
            .AsNoTracking()
            .Include(s => s.LearningPath)
            .Include(s => s.Student)
            .AsQueryable();

        if (isMentor)
        {
            query = query.Where(s => s.MentorId == currentUserId);

            if (request.StudentId.HasValue)
            {
                query = query.Where(s => s.StudentId == request.StudentId.Value);
            }
        }
        else
        {
            query = query.Where(s => s.StudentId == currentUserId);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(s => s.Status == request.Status.Value);
        }

        var hideStatus = isMentor && !request.StudentId.HasValue;

        var shares = await query
            .OrderByDescending(s => s.SentAt)
            .Select(s => new SentLearningPathShareSummaryDto(
                s.ShareId,
                s.PathId,
                s.LearningPath.Title,
                s.LearningPath.Description,
                s.StudentId,
                s.Student.Username,
                hideStatus ? null : s.Status,
                s.SentAt,
                hideStatus ? null : s.RespondedAt
            ))
            .ToListAsync(cancellationToken);

        return Result<List<SentLearningPathShareSummaryDto>>.Success(shares);
    }
}
