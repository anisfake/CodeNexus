using CodeNexus.Application.Common.Models;
using CodeNexus.Application.Features.Users.DTOs;
using MediatR;

namespace CodeNexus.Application.Features.Users.Queries.GetAllUsers;

public record GetAllUsersQuery : IRequest<PaginationDto<UserRespone>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string? Role { get; init; }
    public string? SearchTerm { get; init; }
    public UserSortBy SortBy { get; init; } = UserSortBy.CreatedAt;
    public bool SortDescending { get; init; } = true;
}

public enum UserSortBy
{
    CreatedAt,
    Username,
    Email
}
