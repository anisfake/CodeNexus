using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPaths.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.LearningPathShares.Commands.EnrollInPublishedLearningPath;

public record EnrollInPublishedLearningPathCommand(Guid PathId) : IRequest<Result<EnrollmentResponseDto>>;
