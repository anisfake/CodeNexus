using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.TokenPackages.Commands.DeleteTokenPackage;

public class DeleteTokenPackageCommandHandler : IRequestHandler<DeleteTokenPackageCommand, Result<string>>
{
    private readonly IApplicationDbContext _context;

    public DeleteTokenPackageCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<string>> Handle(DeleteTokenPackageCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.TokenPackages
            .FirstOrDefaultAsync(x => x.TokenPackageId == request.TokenPackageId, cancellationToken);
        if (entity == null)
        {
            return Result<string>.Failure("TOKEN_PACKAGE_NOT_FOUND", "Token package not found.");
        }

        _context.TokenPackages.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return Result<string>.Success("Deleted");
    }
}
