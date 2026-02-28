using CodeNexus.Application.Common.Models;
using MediatR;
using CodeNexus.Application.Features.Goals.DTOs;

namespace CodeNexus.Application.Features.Goals.Commands.CreateGoal;

public record CreateGoalCommand(string Title, string? Description) : IRequest<Result<CreateGoalResponseDto>>;
