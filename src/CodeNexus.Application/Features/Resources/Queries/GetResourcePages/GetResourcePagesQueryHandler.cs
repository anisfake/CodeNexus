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

        public GetResourcePagesQueryHandler(
            IApplicationDbContext context,
            ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }

        public async Task<Result<ResourcePagesResponse>> Handle(GetResourcePagesQuery request, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.GetUserId();

            var resource = await _context.Resources
                .Include(r => r.Pages)
                .FirstOrDefaultAsync(r => r.ResourceId == request.ResourceId && !r.IsDeleted, cancellationToken);

            if (resource == null)
            {
                return Result<ResourcePagesResponse>.Failure("RESOURCE_NOT_FOUND", "Resource not found.");
            }

            if (resource.UserId != userId)
            {
                return Result<ResourcePagesResponse>.Failure("UNAUTHORIZED", "User not authenticated");
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

            return Result<ResourcePagesResponse>.Success(response);
        }
    }
}
