using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Users.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Application.Features.Users.Commands.UpdateProfile
{
    public class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, Result<UserProfileRespone>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        private readonly IAchievementService _achievementService;

        public UpdateProfileCommandHandler(
            IApplicationDbContext context, 
            ICurrentUserService currentUserService,
            IAchievementService achievementService)
        {
            _context = context;
            _currentUserService = currentUserService;
            _achievementService = achievementService;
        }
        public async Task<Result<UserProfileRespone>> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.GetUserId();
            var user = await _context.Users.Include(u => u.UserProfile).FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
            {
                return Result<UserProfileRespone>.Failure("USER_NOT_FOUND", "user was not found.");
            }

            user.FirstName = request.FirstName ?? user.FirstName;
            user.LastName = request.LastName ?? user.LastName;
            user.UserProfile.Bio = request.Bio ?? user.UserProfile.Bio;
            user.UserProfile.DateOfBirth = request.DateOfBirth ?? user.UserProfile.DateOfBirth;
            user.UserProfile.Phone = request.Phone ?? user.UserProfile.Phone;
            user.UserProfile.Address = request.Address ?? user.UserProfile.Address;

            await _context.SaveChangesAsync(cancellationToken);

            var isProfileComplete = !string.IsNullOrEmpty(user.FirstName) && 
                                   !string.IsNullOrEmpty(user.LastName) &&
                                   !string.IsNullOrEmpty(user.UserProfile.Bio) &&
                                   user.UserProfile.DateOfBirth.HasValue &&
                                   !string.IsNullOrEmpty(user.UserProfile.Phone);

            if (isProfileComplete)
            {
                await _achievementService.TryUnlockAsync(userId, "profile_complete");
            }

            return Result<UserProfileRespone>.Success(new UserProfileRespone(
                user.Email,
                user.FirstName,
                user.LastName,
                user.UserProfile.Bio,
                user.Username,
                user.UserProfile.AvatarUrl,
                user.UserProfile.DateOfBirth,
                user.UserProfile.Phone,
                user.UserProfile.Address
            ));
        }
    }
}
