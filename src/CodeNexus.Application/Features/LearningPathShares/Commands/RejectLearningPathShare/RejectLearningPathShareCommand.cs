using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.LearningPathShares.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.LearningPathShares.Commands.RejectLearningPathShare;

public record RejectLearningPathShareCommand(Guid ShareId) : IRequest<Result<LearningPathShareDto>>;
