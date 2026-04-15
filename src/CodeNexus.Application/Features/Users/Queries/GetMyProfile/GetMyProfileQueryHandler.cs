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

namespace CodeNexus.Application.Features.Users.Queries.GetMyProfile
{
    public class GetMyProfileQueryHandler : IRequestHandler<GetMyProfileQuery, Result<UserProfileRespone>>
    {
        private readonly IApplicationDbContext _context;
        private readonly ICurrentUserService _currentUserService;
        public GetMyProfileQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
        }
        public async Task<Result<UserProfileRespone>> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
        {
            var userId = _currentUserService.GetUserId();

            var user = await _context.Users.Include(x => x.UserProfile).FirstOrDefaultAsync(x => x.UserId == userId);

            if (user == null)
            {
                return Result<UserProfileRespone>.Failure("USER_NOT_FOUND", "User not found.");
            }

            return Result<UserProfileRespone>.Success(new UserProfileRespone(
                user.Email,
                user.FirstName ?? string.Empty,
                user.LastName ?? string.Empty,
                user.UserProfile?.Bio ?? string.Empty,
                user.Username,
                user.UserProfile?.AvatarUrl,
                user.UserProfile?.DateOfBirth,
                user.UserProfile?.Phone,
                user.UserProfile?.Address,
                user.UserProfile?.DailyReminderTime,
                user.BalanceVnd
            ));
        }
    }
}
