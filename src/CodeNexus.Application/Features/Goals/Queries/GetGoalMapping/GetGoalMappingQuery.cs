using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Goals.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Goals.Queries.GetGoalMapping;

public record GetGoalMappingQuery(Guid GoalId) : IRequest<Result<GoalMappingDto?>>;
