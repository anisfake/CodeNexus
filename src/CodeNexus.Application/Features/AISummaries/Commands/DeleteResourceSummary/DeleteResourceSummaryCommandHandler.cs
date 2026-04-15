using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.AISummaries.Commands.DeleteResourceSummary;

public class DeleteResourceSummaryCommandHandler : IRequestHandler<DeleteResourceSummaryCommand, Result<string>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public DeleteResourceSummaryCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<string>> Handle(DeleteResourceSummaryCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var summary = await _context.AISummaries
            .Include(x => x.Resource)
            .FirstOrDefaultAsync(x => x.SummaryId == request.SummaryId && !x.IsDeleted, cancellationToken);

        if (summary == null)
        {
            return Result<string>.Failure("SUMMARY_NOT_FOUND", "Summary not found.");
        }

        if (summary.Resource.UserId != userId)
        {
            return Result<string>.Failure("UNAUTHORIZED", "User not authenticated");
        }

        summary.IsDeleted = true;
        summary.DeletedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return Result<string>.Success("Summary deleted successfully");
    }
}
