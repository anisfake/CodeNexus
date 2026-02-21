using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Resources.DTOs;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Resources.Commands.UpdateResource;

public class UpdateResourceCommandHandler : IRequestHandler<UpdateResourceCommand, Result<string>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICloudinaryService _cloudinaryService;

    public UpdateResourceCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICloudinaryService cloudinaryService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cloudinaryService = cloudinaryService;
    }

    public async Task<Result<string>> Handle(UpdateResourceCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.GetUserId();

            var resource = await _context.Resources
                .FirstOrDefaultAsync(x => x.ResourceId == request.ResourceId, cancellationToken);

            if (resource == null)
                return Result<string>.Failure("RESOURCE_NOT_FOUND", "Resource not found");

            if (resource.UserId != userId)
                return Result<string>.Failure("UNAUTHORIZED", "You can only update your own resources");

            if (resource.Type == ResourceType.File && !string.IsNullOrEmpty(request.Url))
                return Result<string>.Failure("INVALID_UPDATE", "Cannot update URL for a File resource. Please upload a file instead.");

            if (resource.Type == ResourceType.Link && request.FilePath != null)
                return Result<string>.Failure("INVALID_UPDATE", "Cannot upload file for a Link resource. Please provide a URL instead.");

            if (!string.IsNullOrEmpty(request.Title))
                resource.Title = request.Title;

            if (request.Description != null)
                resource.Description = request.Description;

            if (resource.Type == ResourceType.File)
            {
                if (request.FilePath != null && !string.IsNullOrEmpty(request.FileName))
                {
                    if (!string.IsNullOrEmpty(resource.FilePath))
                    {
                        var oldPublicId = ExtractPublicIdFromUrl(resource.FilePath);

                        if (!string.IsNullOrEmpty(oldPublicId))
                        {
                            var deleteResult = await _cloudinaryService.DeleteFileAsync(oldPublicId);
                        }
                    }

                    var uploadResult = await _cloudinaryService.UploadFileAsync(
                        request.FilePath,
                        request.FileName,
                        $"resources/{userId}");

                    if (uploadResult == null)
                        return Result<string>.Failure("UPLOAD_FAIL", "File upload failed");

                    resource.FilePath = uploadResult;
                    resource.OriginalFileName = request.FileName;
                }
            }
            else if (resource.Type == ResourceType.Link)
            {
                if (!string.IsNullOrEmpty(request.Url))
                {
                    resource.URL = request.Url;
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
