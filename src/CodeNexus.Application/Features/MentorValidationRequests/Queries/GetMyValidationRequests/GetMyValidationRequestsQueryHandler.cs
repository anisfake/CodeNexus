using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.MentorValidationRequests.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.MentorValidationRequests.Queries.GetMyValidationRequests;

public class GetMyValidationRequestsQueryHandler : IRequestHandler<GetMyValidationRequestsQuery, Result<List<ValidationRequestDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetMyValidationRequestsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<List<ValidationRequestDto>>> Handle(GetMyValidationRequestsQuery request, CancellationToken cancellationToken)
    {
        var studentId = _currentUserService.GetUserId();

        var query = _context.LearningPathValidationRequests
            .AsNoTracking()
            .Include(r => r.LearningPath)
            .Include(r => r.Student)
            .Include(r => r.Mentor)
            .Where(r => r.StudentId == studentId);

        if (request.Status.HasValue)
            query = query.Where(r => r.Status == request.Status.Value);

        var results = await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new ValidationRequestDto(
                r.ValidationRequestId,
                r.PathId,
                r.LearningPath.Title,
                r.StudentId,
                r.Student.Username,
                r.MentorId,
                r.Mentor.Username,
                r.StudentNote,
                r.MentorFeedback,
                r.Status,
                r.CreatedAt,
                r.RespondedAt))
            .ToListAsync(cancellationToken);

        return Result<List<ValidationRequestDto>>.Success(results);
    }
}
