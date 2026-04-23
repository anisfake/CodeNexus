using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.MentorValidationRequests.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;

namespace CodeNexus.Application.Features.MentorValidationRequests.Queries.GetMyValidationRequests;

public record GetMyValidationRequestsQuery(
    ValidationRequestStatus? Status
) : IRequest<Result<List<ValidationRequestDto>>>;
