using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Subjects.Commands.DeleteSubject;

public class DeleteSubjectCommandHandler : IRequestHandler<DeleteSubjectCommand, Result<string>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public DeleteSubjectCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<string>> Handle(DeleteSubjectCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var subject = await _context.Subjects
            .FirstOrDefaultAsync(s => s.SubjectId == request.SubjectId && !s.IsDeleted, cancellationToken);

        if (subject == null)
        {
            return Result<string>.Failure("SUBJECT_NOT_FOUND", "Subject not found.");
        }

        if (subject.CreatedByUserId != userId)
        {
            return Result<string>.Failure("UNAUTHORIZED", "You can only delete subjects you created.");
        }

        try
        {
            subject.IsDeleted = true;
            subject.DeletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure("DELETE_SUBJECT_FAILED", $"An error occurred while deleting the subject: {ex.Message}");
        }

        return Result<string>.Success("Subject deleted successfully.");
    }
}
