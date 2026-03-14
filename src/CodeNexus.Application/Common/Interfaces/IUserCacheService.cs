using CodeNexus.Application.Features.Users.DTOs;

namespace CodeNexus.Application.Common.Interfaces;

public interface IUserCacheService
{
    Task<UserProfileRespone?> GetMyProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SetMyProfileAsync(Guid userId, UserProfileRespone profile, TimeSpan expiration, CancellationToken cancellationToken = default);

    Task<UserRespone?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SetUserByIdAsync(Guid userId, UserRespone user, TimeSpan expiration, CancellationToken cancellationToken = default);

    Task InvalidateUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
