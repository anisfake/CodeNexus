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
        private readonly IPdfProcessingService _pdfProcessingService;
        private readonly IResourceCacheService _resourceCacheService;

        public UploadResourceCommandHandler(
            IApplicationDbContext context,
            ICurrentUserService currentUserService,
            ICloudinaryService cloudinaryService,
            IPdfProcessingService pdfProcessingService,
            IResourceCacheService resourceCacheService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _cloudinaryService = cloudinaryService;
            _pdfProcessingService = pdfProcessingService;
            _resourceCacheService = resourceCacheService;
        }

        public async Task<Result<UploadResourceRespone>> Handle(UploadResourceCommand request, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.GetUserId();

            if (request.FilePath == null || string.IsNullOrEmpty(request.FileName))
            {
                return Result<UploadResourceRespone>.Failure("INVALID_FILE", "File is required.");
            }

            if (!request.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return Result<UploadResourceRespone>.Failure("INVALID_FILE_TYPE", "Only PDF files are allowed.");
            }

            byte[] pdfBytes;
            using (var memoryStream = new MemoryStream())
            {
                await request.FilePath.CopyToAsync(memoryStream, cancellationToken);
                pdfBytes = memoryStream.ToArray();
            }

            string uploadResult;
            using (var uploadStream = new MemoryStream(pdfBytes))
            {
                uploadResult = await _cloudinaryService.UploadFileAsync(uploadStream, request.FileName, $"resources/{userId}");
            }

            if (uploadResult == null)
            {
                return Result<UploadResourceRespone>.Failure("UPLOAD_FAIL", "File upload failed.");
            }

            PdfProcessingResult pdfProcessingResult;
            using (var processStream = new MemoryStream(pdfBytes))
            {
                pdfProcessingResult = await _pdfProcessingService.ProcessPdfAsync(processStream, userId.ToString());
            }

            var resource = new Resource
            {
                ResourceId = NewId.NextGuid(),
                Title = request.Title,
                Type = ResourceType.PDF,
                Description = request.Description,
                FilePath = uploadResult,
                OriginalFileName = request.FileName,
                TotalPages = pdfProcessingResult.TotalPages,
                SubjectId = request.SubjectId,
                UserId = userId,
                UploadedAt = DateTime.UtcNow
            };

            foreach (var pageData in pdfProcessingResult.Pages)
            {
                var resourcePage = new ResourcePage
                {
                    ResourcePageId = NewId.NextGuid(),
                    PageNumber = pageData.PageNumber,
                    ImageUrl = pageData.ImageUrl,
                    ExtractedText = pageData.ExtractedText,
                    CreatedAt = DateTime.UtcNow
                };
                resource.Pages.Add(resourcePage);
            }

            await _context.Resources.AddAsync(resource, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await _resourceCacheService.InvalidateUserResourcesAsync(userId, cancellationToken);

            return Result<UploadResourceRespone>.Success(
                new UploadResourceRespone(
                    resource.ResourceId,
                    resource.Title,
                    resource.Type,
                    resource.Description,
                    resource.FilePath,
                    resource.OriginalFileName,
                    resource.TotalPages
                )
            );
        }
    }
}
