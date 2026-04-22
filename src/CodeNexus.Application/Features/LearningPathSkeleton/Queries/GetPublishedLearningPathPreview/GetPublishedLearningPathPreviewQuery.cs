using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.LearningPathSkeleton.Queries.GetPublishedLearningPathPreview;

public record GetPublishedLearningPathPreviewQuery(Guid PathId) : IRequest<Result<PublishedLearningPathPreviewDto>>;
