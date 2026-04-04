﻿using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Users.Commands.UploadAvatar;

public class UploadAvatarCommandHandler : IRequestHandler<UploadAvatarCommand, Result<string>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICloudinaryService _cloudinaryService;

    public UploadAvatarCommandHandler(IApplicationDbContext context, ICloudinaryService cloudinaryService,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cloudinaryService = cloudinaryService;
    }

    public async Task<Result<string>> Handle(UploadAvatarCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.GetUserId();

        var userProfile = await _context.UserProfiles.Include(x => x.User).FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        if (userProfile == null)
        {
            return Result<string>.Failure("USER_NOT_FOUND", "User not found.");
        }

        if (!string.IsNullOrEmpty(userProfile.AvatarUrl))
        {
            var oldPublicId = ExtractPublicIdFromUrl(userProfile.AvatarUrl);
            if (!string.IsNullOrEmpty(oldPublicId))
            {
                await _cloudinaryService.DeleteImageAsync(oldPublicId);
            }
        }

        var uploadResult = await _cloudinaryService.UploadImageAsync(request.ImageStream, request.FileName, $"avatars/{userProfile.User.Username}");

        if (uploadResult != null)
        {
            userProfile.AvatarUrl = uploadResult;
            await _context.SaveChangesAsync(cancellationToken);
            return Result<string>.Success(uploadResult);
        }

        return Result<string>.Failure("UPLOAD_AVATAR_FAILED", "upload avatar failed");
    }

    private string ExtractPublicIdFromUrl(string cloudinaryUrl)
    {
        try
        {
            var uri = new Uri(cloudinaryUrl);
            var path = uri.AbsolutePath;

            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

            var uploadIndex = Array.IndexOf(parts, "upload");

            if (uploadIndex >= 0 && uploadIndex + 2 < parts.Length)
            {
                var remainingParts = parts.Skip(uploadIndex + 2).ToList();

                if (remainingParts.Count > 0)
                {
                    var lastPart = remainingParts[^1];
                    remainingParts[^1] = System.IO.Path.GetFileNameWithoutExtension(lastPart);
                }

                return string.Join("/", remainingParts);
            }

            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
