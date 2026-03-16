using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathShares.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.LearningPathShares.Queries.GetPendingLearningPathShares;

public record GetPendingLearningPathSharesQuery : IRequest<Result<List<LearningPathShareSummaryDto>>>;
