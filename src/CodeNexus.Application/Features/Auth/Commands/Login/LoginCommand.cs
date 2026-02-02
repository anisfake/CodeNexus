using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Auth.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Auth.Commands.Login;

public record LoginCommand(string Identifier, string Password) : IRequest<Result<LoginResponse>>;
