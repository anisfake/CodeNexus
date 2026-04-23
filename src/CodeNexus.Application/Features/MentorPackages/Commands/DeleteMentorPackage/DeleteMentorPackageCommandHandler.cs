using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.MentorPackages.Commands.DeleteMentorPackage;

public class DeleteMentorPackageCommandHandler : IRequestHandler<DeleteMentorPackageCommand, Result<string>>
{
    private readonly IApplicationDbContext _context;

    public DeleteMentorPackageCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<string>> Handle(DeleteMentorPackageCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.MentorPackages
            .FirstOrDefaultAsync(x => x.MentorPackageId == request.MentorPackageId, cancellationToken);

        if (entity == null)
            return Result<string>.Failure("MENTOR_PACKAGE_NOT_FOUND", "Mentor package not found.");

        _context.MentorPackages.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<string>.Success("Deleted");
    }
}
