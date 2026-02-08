using CodeNexus.Application.Common.Models;
using MediatR;

namespace CodeNexus.Application.Features.Auth.Commands.Logout;

public record LogoutCommand(
    string? AccessToken = null,
    string? RefreshToken = null
) : IRequest<Result>;
