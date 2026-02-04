using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Application.Features.Users.Commands.UploadAvatar
{
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

            var userProfile = await _context.UserProfiles.FirstOrDefaultAsync(x => x.UserId == userId);

            if (userProfile == null)
            {
                return Result<string>.Failure("USER_NOT_FOUND", "user not found");
            }

            var uploadResult = await _cloudinaryService.UploadImageAsync(request.imageStream, request.fileName, "user_avatar");

            if (uploadResult != null)
            {
                userProfile.AvatarUrl = uploadResult;
                await _context.SaveChangesAsync(cancellationToken);
                return Result<string>.Success(uploadResult);
            }

            return Result<string>.Failure("UPLOAD_AVATAR_FAILED", "upload avatar failed");
        }
    }
}
