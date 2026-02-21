using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Resources.Commands.DeleteResource;

public class DeleteResourceCommandHandler : IRequestHandler<DeleteResourceCommand, Result<string>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICloudinaryService _cloudinaryService;

    public DeleteResourceCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ICloudinaryService cloudinaryService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cloudinaryService = cloudinaryService;
    }

    public async Task<Result<string>> Handle(DeleteResourceCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.GetUserId();

            var resource = await _context.Resources
                .FirstOrDefaultAsync(x => x.ResourceId == request.ResourceId, cancellationToken);

            if (resource == null)
                return Result<string>.Failure("RESOURCE_NOT_FOUND", "Resource not found");

            if (resource.UserId != userId)
                return Result<string>.Failure("UNAUTHORIZED", "You can only delete your own resources");

            if (resource.Type == ResourceType.File && !string.IsNullOrEmpty(resource.FilePath))
            {
                var publicId = ExtractPublicIdFromUrl(resource.FilePath);
                if (!string.IsNullOrEmpty(publicId))
                {
                    await _cloudinaryService.DeleteFileAsync(publicId);
                }
            }

            _context.Resources.Remove(resource);
            await _context.SaveChangesAsync(cancellationToken);

            return Result<string>.Success("Resource deleted successfully");
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
        catch
        {
            return string.Empty;
        }
    }
}
