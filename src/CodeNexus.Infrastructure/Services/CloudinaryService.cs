using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeNexus.Infrastructure.Services;

public class CloudinaryService : ICloudinaryService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryService(IOptions<CloudinarySettings> options)
    {
        var settings = options.Value;

        if (string.IsNullOrEmpty(settings?.CloudName) ||
            string.IsNullOrEmpty(settings?.ApiKey) ||
            string.IsNullOrEmpty(settings?.ApiSecret))
        {
            throw new InvalidOperationException("Cloudinary settings are not properly configured in appsettings.json");
        }

        var account = new Account(
            settings.CloudName,
            settings.ApiKey,
            settings.ApiSecret
        );
        _cloudinary = new Cloudinary(account);
    }

    public async Task<string> UploadImageAsync(Stream imageStream, string fileName, string folder)
    {
        try
        {
            var uploadParams = new ImageUploadParams()
            {
                File = new FileDescription(fileName, imageStream),
                Folder = folder,
                PublicId = Guid.NewGuid().ToString()
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
            {
                throw new InvalidOperationException($"Upload failed: {uploadResult.Error.Message}");
            }

            return uploadResult.SecureUrl.ToString();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<bool> DeleteImageAsync(string publicId)
    {
        try
        {
            var deleteParams = new DeletionParams(publicId);
            var result = await _cloudinary.DestroyAsync(deleteParams);

            if (result.Error != null)
            {
                return false;
            }

            return result.Result == "ok";
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<string> UploadFileAsync(Stream file, string fileName, string folder)
    {
        try
        {
            var uploadParams = new RawUploadParams()
            {
                File = new FileDescription(fileName, file),
                Folder = folder,
                PublicId = Guid.NewGuid().ToString()
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);

            if (uploadResult.Error != null)
            {
                throw new InvalidOperationException($"Upload failed: {uploadResult.Error.Message}");
            }

            return uploadResult.SecureUrl.ToString();
        }
        catch (Exception ex)
        {
            throw;
        }
    }

    public async Task<bool> DeleteFileAsync(string publicId)
    {
        try
        {
            Console.WriteLine($"[CloudinaryService] Attempting to delete file with publicId: {publicId}");
            
            // Determine resource type based on file extension
            var resourceType = ResourceType.Raw;
            var cleanPublicId = publicId;
            
            // Check if it's an image file
            var isImage = publicId.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                         publicId.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                         publicId.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                         publicId.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ||
                         publicId.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
            
            if (isImage)
            {
                resourceType = ResourceType.Image;
            }
            
            // Always remove extension from public ID (Cloudinary stores without extension)
            var lastDotIndex = publicId.LastIndexOf('.');
            if (lastDotIndex > 0)
            {
                cleanPublicId = publicId.Substring(0, lastDotIndex);
            }
            
            Console.WriteLine($"[CloudinaryService] Using resourceType: {resourceType}, cleanPublicId: {cleanPublicId}");
            
            var deleteParams = new DeletionParams(cleanPublicId)
            {
                ResourceType = resourceType
            };
            var result = await _cloudinary.DestroyAsync(deleteParams);

            Console.WriteLine($"[CloudinaryService] Delete result: {result.Result}");
            Console.WriteLine($"[CloudinaryService] Delete error: {result.Error?.Message ?? "None"}");

            if (result.Error != null)
            {
                return false;
            }

            return result.Result == "ok";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CloudinaryService] Delete exception: {ex.Message}");
            throw;
        }
    }
}
