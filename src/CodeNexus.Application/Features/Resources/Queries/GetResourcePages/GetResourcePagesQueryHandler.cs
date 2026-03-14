using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Resources.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CodeNexus.Application.Features.Resources.Queries.GetResourcePages
{
    public class GetResourcePagesQueryHandler : IRequestHandler<GetResourcePagesQuery, Result<ResourcePagesResponse>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IResourceCacheService _resourceCacheService;

        public GetResourcePagesQueryHandler(
            IApplicationDbContext context,
            ICurrentUserService currentUserService,
            IResourceCacheService resourceCacheService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _resourceCacheService = resourceCacheService;
        }

        public async Task<Result<ResourcePagesResponse>> Handle(GetResourcePagesQuery request, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.GetUserId();

            var cachedPages = await _resourceCacheService.GetResourcePagesAsync(userId, request.ResourceId, cancellationToken);
            if (cachedPages != null)
            {
                return Result<ResourcePagesResponse>.Success(cachedPages);
            }

            var resource = await _context.Resources
                .Include(r => r.Pages)
                .FirstOrDefaultAsync(r => r.ResourceId == request.ResourceId && !r.IsDeleted, cancellationToken);

            if (resource == null)
            {
                return Result<ResourcePagesResponse>.Failure("RESOURCE_NOT_FOUND", "Resource not found.");
            }

            if (resource.UserId != userId)
            {
                return Result<ResourcePagesResponse>.Failure("UNAUTHORIZED", "You can only view your own resources.");
            }

            var pages = resource.Pages
                .OrderBy(p => p.PageNumber)
                .Select(p => new ResourcePageDto(
                    p.ResourcePageId,
                    p.PageNumber,
                    p.ImageUrl,
                    p.ExtractedText
                ))
                .ToList();

            var response = new ResourcePagesResponse(
                resource.ResourceId,
                resource.Title,
                resource.OriginalFileName,
                resource.TotalPages ?? 0,
                pages
            );

            await _resourceCacheService.SetResourcePagesAsync(userId, request.ResourceId, response, TimeSpan.FromMinutes(3), cancellationToken);

            return Result<ResourcePagesResponse>.Success(response);
        }
    }
}
