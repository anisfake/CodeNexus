using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Resources.DTOs;
using CodeNexus.Domain.Entities;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CodeNexus.Application.Features.Resources.Commands.UpdateResource;

public class UpdateResourceCommandHandler : IRequestHandler<UpdateResourceCommand, Result<string>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICloudinaryService _cloudinaryService;
    private readonly IPdfProcessingService _pdfProcessingService;

    public UpdateResourceCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICloudinaryService cloudinaryService,
        IPdfProcessingService pdfProcessingService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cloudinaryService = cloudinaryService;
        _pdfProcessingService = pdfProcessingService;
    }

    public async Task<Result<string>> Handle(UpdateResourceCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.GetUserId();

            var resource = await _context.Resources
                .Include(r => r.Pages)
                .FirstOrDefaultAsync(x => x.ResourceId == request.ResourceId, cancellationToken);

            if (resource == null)
                return Result<string>.Failure("RESOURCE_NOT_FOUND", "Resource not found");

            if (resource.UserId != userId)
                return Result<string>.Failure("UNAUTHORIZED", "You can only update your own resources");

            if (!string.IsNullOrEmpty(request.Title))
                resource.Title = request.Title;

            if (request.Description != null)
                resource.Description = request.Description;

            if (request.FilePath != null && !string.IsNullOrEmpty(request.FileName))
            {
                if (!request.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    return Result<string>.Failure("INVALID_FILE_TYPE", "Only PDF files are allowed.");

                if (resource.Pages != null && resource.Pages.Any())
                {
                    foreach (var page in resource.Pages)
                    {
                        if (!string.IsNullOrEmpty(page.ImageUrl))
                        {
                            var pagePublicId = ExtractPublicIdFromUrl(page.ImageUrl);
                            if (!string.IsNullOrEmpty(pagePublicId))
                            {
                                await _cloudinaryService.DeleteFileAsync(pagePublicId);
                            }
                        }
                    }

                    _context.ResourcePages.RemoveRange(resource.Pages);
                }

                if (!string.IsNullOrEmpty(resource.FilePath))
                {
                    var oldPublicId = ExtractPublicIdFromUrl(resource.FilePath);
                    if (!string.IsNullOrEmpty(oldPublicId))
                    {
                        await _cloudinaryService.DeleteFileAsync(oldPublicId);
                    }
                }

                // Read stream into byte array to avoid stream disposal issues
                byte[] fileBytes;
                if (request.FilePath.CanSeek)
                {
                    request.FilePath.Position = 0;
                }
                
                using (var ms = new System.IO.MemoryStream())
                {
                    await request.FilePath.CopyToAsync(ms, cancellationToken);
                    fileBytes = ms.ToArray();
                }

                // Upload new PDF file
                using (var uploadStream = new System.IO.MemoryStream(fileBytes))
                {
                    var uploadResult = await _cloudinaryService.UploadFileAsync(
                        uploadStream,
                        request.FileName,
                        $"resources/{userId}");

                    if (uploadResult == null)
                        return Result<string>.Failure("UPLOAD_FAIL", "File upload failed");

                    resource.FilePath = uploadResult;
                    resource.OriginalFileName = request.FileName;
                }

                // Process new PDF and create new pages
                using (var processStream = new System.IO.MemoryStream(fileBytes))
                {
                    var processingResult = await _pdfProcessingService.ProcessPdfAsync(processStream, userId.ToString());

                    resource.TotalPages = processingResult.TotalPages;

                    var newPages = processingResult.Pages.Select(p => new ResourcePage
                    {
                        ResourcePageId = Guid.NewGuid(),
                        ResourceId = resource.ResourceId,
                        PageNumber = p.PageNumber,
                        ImageUrl = p.ImageUrl,
                        ExtractedText = p.ExtractedText,
                        CreatedAt = DateTime.UtcNow
                    }).ToList();

                    await _context.ResourcePages.AddRangeAsync(newPages, cancellationToken);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            return Result<string>.Success("Update resource successfully");
        }
        catch (Exception ex)
        {
            return Result<string>.Failure("ERROR", ex.Message);
        }
    }

    private string ExtractPublicIdFromUrl(string cloudinaryUrl)
    {
        try
        {
            var uri = new Uri(cloudinaryUrl);
            var path = uri.AbsolutePath;

            var parts = path.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

            var uploadIndex = Array.IndexOf(parts, "upload");

            if (uploadIndex >= 0 && uploadIndex + 1 < parts.Length)
            {
                var startIndex = uploadIndex + 1;

                if (startIndex < parts.Length && parts[startIndex].StartsWith("v") &&
                    parts[startIndex].Length > 1 && char.IsDigit(parts[startIndex][1]))
                {
                    startIndex++;
                }

                if (startIndex < parts.Length)
                {
                    var remainingParts = parts.Skip(startIndex).ToList();

                    return string.Join("/", remainingParts);
                }
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG] ExtractPublicIdFromUrl exception: {ex.Message}");
            return string.Empty;
        }
    }
}
