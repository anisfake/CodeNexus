using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathShares.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;

namespace CodeNexus.Application.Features.LearningPathShares.Queries.GetSentLearningPathShares;

public record GetSentLearningPathSharesQuery(
    LearningPathShareStatus? Status = null,
    Guid? StudentId = null,
    Guid? PathId = null
) : IRequest<Result<List<SentLearningPathShareSummaryDto>>>;
