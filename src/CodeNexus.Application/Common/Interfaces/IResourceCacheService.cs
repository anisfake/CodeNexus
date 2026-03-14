using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Resources.DTOs;

namespace CodeNexus.Application.Common.Interfaces;

public interface IResourceCacheService
{
    Task<PaginationDto<ResourceResponse>?> GetMyResourcesAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        Guid? subjectId,
        string? searchTerm,
        int sortBy,
        bool sortDescending,
        CancellationToken cancellationToken = default);

    Task SetMyResourcesAsync(
        Guid userId,
        int pageNumber,
        int pageSize,
        Guid? subjectId,
        string? searchTerm,
        int sortBy,
        bool sortDescending,
        PaginationDto<ResourceResponse> data,
        TimeSpan expiration,
        CancellationToken cancellationToken = default);

    Task<ResourcePagesResponse?> GetResourcePagesAsync(Guid userId, Guid resourceId, CancellationToken cancellationToken = default);
    Task SetResourcePagesAsync(Guid userId, Guid resourceId, ResourcePagesResponse data, TimeSpan expiration, CancellationToken cancellationToken = default);

    Task InvalidateUserResourcesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task InvalidateResourcePagesAsync(Guid userId, Guid resourceId, CancellationToken cancellationToken = default);
}
