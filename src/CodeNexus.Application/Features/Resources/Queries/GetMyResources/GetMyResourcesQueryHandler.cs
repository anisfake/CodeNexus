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

        public GetMyResourcesQueryHandler(
            IApplicationDbContext context,
            ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<PaginationDto<ResourceResponse>> Handle(GetMyResourcesQuery request, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.GetUserId();

            var query = _context.Resources
                .Where(r => r.UserId == userId)
                .Include(r => r.Subject)
                .AsQueryable();

            // filter subject
            if (request.SubjectId.HasValue && request.SubjectId != Guid.Empty)
            {
                query = query.Where(r => r.SubjectId == request.SubjectId);
            }

            // filter type
            if (!request.Type.Equals(ResourceType.All))
            {
                query = query.Where(r => r.Type.Equals(request.Type));
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

            // pagination
            var items = await query
                .Skip((request.PageNumber - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(r => new ResourceResponse
                (
                    r.ResourceId,
                    r.Title,
                    r.Type,
                    r.URL,
                    r.Description,
                    r.FilePath,
                    r.OriginalFileName,
                    r.Subject.Name,
                    r.SubjectId
                ))
                .ToListAsync(cancellationToken);

            return new PaginationDto<ResourceResponse>
            {
                Items = items,
                PageNumber = request.PageNumber,
                PageSize = request.PageSize,
                TotalCount = totalCount
            };
        }
    }
}
