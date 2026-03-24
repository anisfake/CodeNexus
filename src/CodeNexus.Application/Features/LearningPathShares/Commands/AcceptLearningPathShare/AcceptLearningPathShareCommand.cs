using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathShares.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.LearningPathShares.Commands.AcceptLearningPathShare;

public record AcceptLearningPathShareCommand(Guid ShareId) : IRequest<Result<LearningPathShareDto>>;
