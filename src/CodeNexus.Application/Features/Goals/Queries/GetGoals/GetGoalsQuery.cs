using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Goals.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Goals.Queries.GetGoals;

public record GetGoalsQuery : IRequest<Result<List<GoalDto>>>;
