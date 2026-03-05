using CodeNexus.Application.Common.Models;
using MediatR;

namespace CodeNexus.Application.Features.Users.Commands.BanUser;

public record BanUserCommand(Guid UserId) : IRequest<Result<string>>;
