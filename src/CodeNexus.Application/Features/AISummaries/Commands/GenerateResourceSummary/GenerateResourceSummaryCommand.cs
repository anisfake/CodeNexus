using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.AISummaries.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.AISummaries.Commands.GenerateResourceSummary;

public record GenerateResourceSummaryCommand(
    Guid ResourceId,
    int StartPage,
    int EndPage
) : IRequest<Result<ResourceSummaryDto>>;
