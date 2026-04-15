using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.AISummaries.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.AISummaries.Queries.GetResourceSummaries;

public record GetResourceSummariesQuery(Guid ResourceId) : IRequest<Result<List<ResourceSummaryDto>>>;
