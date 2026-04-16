using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathShares.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.LearningPathShares.Queries.GetLearningPathShareUpdateContext;

public record GetLearningPathShareUpdateContextQuery(Guid ShareId)
    : IRequest<Result<LearningPathShareUpdateContextDto>>;
