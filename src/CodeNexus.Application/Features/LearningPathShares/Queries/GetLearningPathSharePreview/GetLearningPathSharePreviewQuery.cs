using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathShares.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.LearningPathShares.Queries.GetLearningPathSharePreview;

public record GetLearningPathSharePreviewQuery(Guid ShareId) : IRequest<Result<LearningPathSharePreviewDto>>;
