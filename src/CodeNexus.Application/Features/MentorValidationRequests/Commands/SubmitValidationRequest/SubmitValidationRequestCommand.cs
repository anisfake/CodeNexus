using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.MentorValidationRequests.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.MentorValidationRequests.Commands.SubmitValidationRequest;

public record SubmitValidationRequestCommand(
    Guid PathId,
    Guid MentorId,
    string? StudentNote
) : IRequest<Result<ValidationRequestDto>>;
