using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Users.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CodeNexus.Application.Features.Users.Queries.GetAllUsers;

public class GetAllUsersQueryHandler : IRequestHandler<GetAllUsersQuery, PaginationDto<UserRespone>>
{
    private readonly IApplicationDbContext _context;

    public GetAllUsersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginationDto<UserRespone>> Handle(GetAllUsersQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Users
            .Include(u => u.Role)
            .Include(u => u.UserProfile)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            query = query.Where(u => u.Role != null && u.Role.RoleName.ToLower() == request.Role.ToLower());
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var searchTerm = request.SearchTerm.ToLower();
            query = query.Where(u =>
                u.Username.ToLower().Contains(searchTerm) ||
                u.Email.ToLower().Contains(searchTerm) ||
                (u.FirstName != null && u.FirstName.ToLower().Contains(searchTerm)) ||
                (u.LastName != null && u.LastName.ToLower().Contains(searchTerm))
            );
        }

        var totalCount = await query.CountAsync(cancellationToken);

        query = request.SortBy switch
        {
            UserSortBy.Username => request.SortDescending
                ? query.OrderByDescending(u => u.Username)
                : query.OrderBy(u => u.Username),
            UserSortBy.Email => request.SortDescending
                ? query.OrderByDescending(u => u.Email)
                : query.OrderBy(u => u.Email),
            _ => request.SortDescending
                ? query.OrderByDescending(u => u.CreatedAt)
                : query.OrderBy(u => u.CreatedAt)
        };

        var items = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(u => new UserRespone(
                u.UserId,
                u.Username,
                u.Email,
                u.FirstName,
                u.LastName,
                u.UserProfile != null ? u.UserProfile.AvatarUrl : null,
                u.UserProfile != null ? u.UserProfile.Bio : null,
                u.UserProfile != null ? u.UserProfile.Phone : null,
                u.UserProfile != null ? u.UserProfile.Address : null,
                u.UserProfile != null ? u.UserProfile.DateOfBirth : null,
                u.Role != null ? u.Role.RoleName : null,
                u.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return new PaginationDto<UserRespone>
        {
            Items = items,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalCount = totalCount
        };
    }
}
