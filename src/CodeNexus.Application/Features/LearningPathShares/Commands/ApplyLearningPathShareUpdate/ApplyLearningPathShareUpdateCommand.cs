using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathShares.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.LearningPathShares.Commands.ApplyLearningPathShareUpdate;

public record ApplyLearningPathShareUpdateCommand(
    Guid ShareId,
    LearningPathShareUpdateAction Action
) : IRequest<Result<LearningPathShareDto>>;
