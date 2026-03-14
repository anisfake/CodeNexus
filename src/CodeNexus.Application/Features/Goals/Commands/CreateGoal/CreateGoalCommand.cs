using CodeNexus.Application.Common.Models;
using MediatR;
using CodeNexus.Application.Features.Goals.DTOs;
using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.Goals.Commands.CreateGoal;

public record CreateGoalCommand(string Title, string? Description, GoalDuration Duration) : IRequest<Result<CreateGoalResponseDto>>;
