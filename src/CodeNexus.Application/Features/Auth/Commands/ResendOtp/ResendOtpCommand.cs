using CodeNexus.Application.Common.Models;
using MediatR;

namespace CodeNexus.Application.Features.Auth.Commands.ResendOtp;

public record ResendOtpCommand(string Email) : IRequest<Result>;
