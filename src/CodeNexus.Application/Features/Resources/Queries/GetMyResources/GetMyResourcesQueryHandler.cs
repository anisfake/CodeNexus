using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Resources.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CodeNexus.Domain.Enums;

namespace CodeNexus.Application.Features.Resources.Queries.GetMyResources
{
    public class GetMyResourcesQueryHandler : IRequestHandler<GetMyResourcesQuery, PaginationDto<ResourceResponse>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IResourceCacheService _resourceCacheService;

        public GetMyResourcesQueryHandler(
            IApplicationDbContext context,
            ICurrentUserService currentUserService,
            IResourceCacheService resourceCacheService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _resourceCacheService = resourceCacheService;
        }

        public async Task<PaginationDto<ResourceResponse>> Handle(GetMyResourcesQuery request, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.GetUserId();

            var cachedData = await _resourceCacheService.GetMyResourcesAsync(
                userId,
                request.PageNumber,
                request.PageSize,
                request.SubjectId,
                request.SearchTerm,
                (int)request.SortBy,
                request.SortDescending,
                cancellationToken);

            if (cachedData != null)
            {
                return cachedData;
            }

            var query = _context.Resources
                .Where(r => r.UserId == userId && !r.IsDeleted)
                .AsQueryable();

            // filter subject
            if (request.SubjectId.HasValue && request.SubjectId != Guid.Empty)
            {
                query = query.Where(r => r.SubjectId == request.SubjectId);
            }

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var searchTerm = request.SearchTerm.ToLower();
                query = query.Where(r =>
                    r.Title.ToLower().Contains(searchTerm) || (r.Description != null
                    && r.Description.ToLower().Contains(searchTerm))
                );
            }

            var totalCount = await query.CountAsync(cancellationToken);

            // sort
            query = request.SortBy switch
            {
                ResourceSortBy.Title => request.SortDescending
                    ? query.OrderByDescending(r => r.Title)
                    : query.OrderBy(r => r.Title),
                _ => request.SortDescending
                    ? query.OrderByDescending(r => r.UploadedAt)
                    : query.OrderBy(r => r.UploadedAt)
            };

            // pagination and projection
            var items = await query
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(r => new ResourceResponse
                (
                    r.ResourceId,
                    r.Title,
                    r.Type,
                    r.Description ?? string.Empty,
                    r.FilePath,
                    r.OriginalFileName,
                    r.TotalPages,
                    r.Subject != null ? r.Subject.Name : "Unknown",
                    r.SubjectId
                ))
                .ToListAsync(cancellationToken);

            var response = new PaginationDto<ResourceResponse>
            {
                Items = items,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                TotalCount = totalCount
            };

            await _resourceCacheService.SetMyResourcesAsync(
                userId,
                request.PageNumber,
                request.PageSize,
                request.SubjectId,
                request.SearchTerm,
                (int)request.SortBy,
                request.SortDescending,
                response,
                TimeSpan.FromMinutes(3),
                cancellationToken);

            return response;
        }
    }
}
