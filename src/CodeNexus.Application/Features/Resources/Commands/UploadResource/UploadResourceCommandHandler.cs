using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Resources.DTOs;
using CodeNexus.Domain.Entities;
using MassTransit;
using MediatR;
using CodeNexus.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Application.Features.Resources.Commands.UploadResource
{
    public class UploadResourceCommandHandler : IRequestHandler<UploadResourceCommand, Result<UploadResourceRespone>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly ICloudinaryService _cloudinaryService;
        public UploadResourceCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService,
            ICloudinaryService cloudinaryService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _cloudinaryService = cloudinaryService;
        }
        public async Task<Result<UploadResourceRespone>> Handle(UploadResourceCommand request, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.GetUserId();

            var resource = new Resource
            {
                Title = request.Title,
                Type = request.Type,
                URL = "",
                Description = request.Description,
                FilePath = null,
                SubjectId = request.SubjectId,
                UserId = userId,
                UploadedAt = DateTime.Now
            };

            if (request.Type == ResourceType.File && request.FilePath != null)
            {
                var uploadResult = await _cloudinaryService.UploadFileAsync(request.FilePath, request.FileName, $"resources/{userId}");

                if (uploadResult == null)
                {
                    return Result<UploadResourceRespone>.Failure("UPLOAD_FAIL", "File upload failed.");

                }

                resource.FilePath = uploadResult;
                resource.OriginalFileName = request.FileName;
            }
            else if (request.Type == ResourceType.Link && !string.IsNullOrEmpty(request.Url))
            {
                resource.URL = request.Url;
            }
            else
            {
                return Result<UploadResourceRespone>.Failure("INVALID_RESOURCE", "Invalid resource data.");
            }

            await _context.Resources.AddAsync(resource, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<UploadResourceRespone>.Success(
                new UploadResourceRespone(
                    resource.Title,
                    resource.Type,
                    resource.URL,
                    resource.Description,
                    resource.FilePath,
                    resource.OriginalFileName
                )
            );
        }
    }
}
