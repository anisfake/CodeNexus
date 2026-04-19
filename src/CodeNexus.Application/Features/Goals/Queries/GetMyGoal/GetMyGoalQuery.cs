using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Goals.DTOs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Application.Features.Goals.Queries.GetMyGoal
{
    public record GetMyGoalQuery : IRequest<Result<PaginationDto<GetMyGoalGoalResponse>>>
    {
        public int PageNumber { get; init; } = 1;
        public int PageSize { get; init; } = 10;
        public string? SearchTerm { get; init; }
        public bool SortDescending { get; init; } = true;
    }
}
