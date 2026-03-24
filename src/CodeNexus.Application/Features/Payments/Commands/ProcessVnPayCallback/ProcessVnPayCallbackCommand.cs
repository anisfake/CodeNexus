using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Payments.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Payments.Commands.ProcessVnPayCallback;

public record ProcessVnPayCallbackCommand(
    IDictionary<string, string> Parameters) : IRequest<Result<VnPayCallbackResponseDto>>;
