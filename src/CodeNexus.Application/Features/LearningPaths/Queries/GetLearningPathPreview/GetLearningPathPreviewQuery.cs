using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.LearningPaths.Queries.GetLearningPathPreview;

public record GetLearningPathPreviewQuery(Guid PathId) : IRequest<Result<LearningPathResponse>>;

