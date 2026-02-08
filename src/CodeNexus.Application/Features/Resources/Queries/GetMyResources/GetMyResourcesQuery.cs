using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Resources.DTOs;
using CodeNexus.Domain.Enums;
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
        public ResourceType? Type { get; init; }
        public Guid? SubjectId { get; init; }
        public string? SearchTerm { get; init; }
        public ResourceSortBy SortBy { get; init; }
        public bool SortDescending { get; init; } = true;
    }
    public enum ResourceSortBy
    {
        UploadedAt,
        Title
    }
}
