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
        Guid ResourceId,
        string Title,
        ResourceType Type,
        string? Description,
        string? FilePath,
        string? OriginalFileName,
        int? TotalPages,
        string SubjectName,
        Guid SubjectId
    );

    public record UploadResourceRespone(
        Guid ResourceId,
        string Title,
        ResourceType Type,
        string? Description,
        string? FilePath,
        string? OriginalFileName,
        int? TotalPages
    );

    public record GetMyResourceRequest(
        int PageNumber = 1,
        int PageSize = 10,
        Guid? SubjectId = null,
        string? SearchTerm = null,
        ResourceSortBy SortBy = ResourceSortBy.Title,
        bool SortDescending = true
    );

    public record ResourcePagesResponse(
        Guid ResourceId,
        string Title,
        string? OriginalFileName,
        int TotalPages,
        List<ResourcePageDto> Pages
    );

    public record ResourcePageDto(
        Guid ResourcePageId,
        int PageNumber,
        string ImageUrl,
        string? ExtractedText
    );
}
