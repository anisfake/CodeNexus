namespace CodeNexus.Application.Common.Interfaces;

public interface ITokenBlacklistCacheService
{
    Task<bool?> GetTokenStatusAsync(string tokenId, CancellationToken cancellationToken = default);
    Task SetTokenStatusAsync(string tokenId, bool isBlacklisted, TimeSpan expiration, CancellationToken cancellationToken = default);
}
