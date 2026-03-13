using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Auth.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Auth.Commands.RefreshAccessToken;

public record RefreshAccessTokenCommand(string RefreshToken) : IRequest<Result<LoginResponse>>;
