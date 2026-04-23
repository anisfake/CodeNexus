using CodeNexus.Application.Common.Models;
using MediatR;

namespace CodeNexus.Application.Features.MentorValidationRequests.Commands.RespondToValidationRequest;

public record RespondToValidationRequestCommand(
    Guid ValidationRequestId,
    string? Feedback,
    bool Accept
) : IRequest<Result>;
