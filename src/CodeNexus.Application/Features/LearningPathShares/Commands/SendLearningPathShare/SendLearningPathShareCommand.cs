using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathShares.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.LearningPathShares.Commands.SendLearningPathShare;

public record SendLearningPathShareCommand(
    Guid PathId,
    Guid StudentId
) : IRequest<Result<LearningPathShareDto>>;
