using CodeNexus.Application.Common.Models;
using MediatR;

namespace CodeNexus.Application.Features.Users.Commands.UnbanUser;

public record UnbanUserCommand(Guid UserId) : IRequest<Result<string>>;
