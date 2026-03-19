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
        Guid mentorId;
        try
        {
            mentorId = _currentUserService.GetUserId();
        }
        catch
        {
            return Result<List<SentLearningPathShareSummaryDto>>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        var mentor = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.UserId == mentorId, cancellationToken);

        if (mentor == null)
        {
            return Result<List<SentLearningPathShareSummaryDto>>.Failure("USER_NOT_FOUND", "User not found.");
        }

        if (!string.Equals(mentor.Role?.RoleName, "Mentor", StringComparison.OrdinalIgnoreCase))
        {
            return Result<List<SentLearningPathShareSummaryDto>>.Failure("ACCESS_DENIED", "Only mentors can view sent learning path shares.");
        }

        var query = _context.LearningPathShares
            .AsNoTracking()
            .Where(s => s.MentorId == mentorId)
            .Include(s => s.LearningPath)
            .Include(s => s.Student)
            .AsQueryable();

        if (request.Status.HasValue)
        {
            query = query.Where(s => s.Status == request.Status.Value);
        }

        if (request.StudentId.HasValue)
        {
            query = query.Where(s => s.StudentId == request.StudentId.Value);
        }

        var shares = await query
            .OrderByDescending(s => s.SentAt)
            .Select(s => new SentLearningPathShareSummaryDto(
                s.ShareId,
                s.PathId,
                s.LearningPath.Title,
                s.LearningPath.Description,
                s.StudentId,
                s.Student.Username,
                s.Status,
                s.SentAt,
                s.RespondedAt
            ))
            .ToListAsync(cancellationToken);

        return Result<List<SentLearningPathShareSummaryDto>>.Success(shares);
    }
}
