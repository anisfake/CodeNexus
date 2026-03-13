using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Users.Commands.UnbanUser;

public class UnbanUserCommandHandler : IRequestHandler<UnbanUserCommand, Result<string>>
{
    private readonly IApplicationDbContext _context;

    public UnbanUserCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<string>> Handle(UnbanUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == request.UserId, cancellationToken);

        if (user == null)
        {
            return Result<string>.Failure("USER_NOT_FOUND", "User not found.");
        }

        if (user.Status != "Banned")
        {
            return Result<string>.Failure("USER_NOT_BANNED", "User is not banned.");
        }

        user.Status = "Active";
        await _context.SaveChangesAsync(cancellationToken);

        return Result<string>.Success($"Successfully lifted the ban on user {user.Username}");
    }
}
