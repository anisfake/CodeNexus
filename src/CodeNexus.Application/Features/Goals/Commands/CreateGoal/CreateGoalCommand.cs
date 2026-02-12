using CodeNexus.Application.Common.Models;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CodeNexus.Application.Features.Goals.DTOs;

namespace CodeNexus.Application.Features.Goals.Commands.CreateGoal;

public record CreateGoalCommand(string Title, string? Description, int DurationsDay) : IRequest<Result<CreateGoalResponeDto>>;
