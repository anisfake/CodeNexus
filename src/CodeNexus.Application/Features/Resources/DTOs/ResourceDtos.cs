using CodeNexus.Application.Features.Resources.Queries.GetMyResources;
using CodeNexus.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Application.Features.Resources.DTOs
{
    public record ResourceResponse(
        string Title,
        ResourceType Type,
        string? Url,
        string? Description,
        string? FilePath,
        string SubjectName,
        Guid SubjectId
    );

    public record UploadResourceRespone(
        string Title,
        ResourceType Type,
        string? Url,
        string? Description,
        string? FilePath
    );

    public record GetMyResourceRequest(
        int PageNumber = 1,
        int PageSize = 10,
        ResourceType Type = ResourceType.File,
        Guid? SubjectId = null,
        string? SearchTerm = null,
        ResourceSortBy SortBy = ResourceSortBy.Title,
        bool SortDescending = true
    );
}
