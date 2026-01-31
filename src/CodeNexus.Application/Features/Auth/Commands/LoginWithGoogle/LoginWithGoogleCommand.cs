using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Auth.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Auth.Commands.LoginWithGoogle;

public record LoginWithGoogleCommand(string IdToken) : IRequest<Result<LoginResponse>>;
