using CodeNexus.Application.Common.Models;
using MediatR;

namespace CodeNexus.Application.Features.AISummaries.Commands.DeleteResourceSummary;

public record DeleteResourceSummaryCommand(Guid SummaryId) : IRequest<Result<string>>;
