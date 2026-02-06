using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Resources.DTOs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Application.Features.Resources.Queries.GetMyResources
{
    public record GetMyResourcesQuery : IRequest<PaginationDto<ResourceResponse>>
    {
        public int PageNumber { get; init; } = 1;
        public int PageSize { get; init; } = 10;
        public string? Type { get; init; }
        public Guid? SubjectId { get; init; }
        public string? SearchTerm { get; init; }
        public string SortBy { get; init; } = "CreatedAt";
        public bool SortDescending { get; init; } = true;
    }
}
