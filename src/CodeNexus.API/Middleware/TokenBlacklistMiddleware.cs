using CodeNexus.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;

namespace CodeNexus.API.Middleware;

public class TokenBlacklistMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly TimeSpan NonBlacklistedCacheDuration = TimeSpan.FromSeconds(30);

    public TokenBlacklistMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IApplicationDbContext dbContext, ITokenBlacklistCacheService tokenBlacklistCacheService)
    {
        var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();

        if (!string.IsNullOrEmpty(token))
        {
            var handler = new JwtSecurityTokenHandler();

            if (handler.CanReadToken(token))
            {
                var jwtToken = handler.ReadJwtToken(token);
                var jti = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;

                if (!string.IsNullOrEmpty(jti))
                {
                    var cachedStatus = await tokenBlacklistCacheService.GetTokenStatusAsync(jti);
                    if (cachedStatus.HasValue)
                    {
                        if (cachedStatus.Value)
                        {
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            await context.Response.WriteAsJsonAsync(new
                            {
                                ErrorCode = "TOKEN_BLACKLISTED",
                                ErrorMessage = "This token has been revoked"
                            });
                            return;
                        }

                        await _next(context);
                        return;
                    }

                    var blacklistExpiresAt = await dbContext.TokenBlacklist
                        .AsNoTracking()
                        .Where(t => t.TokenId == jti && t.ExpiresAt > DateTime.UtcNow)
                        .Select(t => (DateTime?)t.ExpiresAt)
                        .FirstOrDefaultAsync();

                    var isBlacklisted = blacklistExpiresAt.HasValue;

                    if (isBlacklisted)
                    {
                        var remaining = blacklistExpiresAt!.Value - DateTime.UtcNow;
                        if (remaining > TimeSpan.Zero)
                        {
                            await tokenBlacklistCacheService.SetTokenStatusAsync(jti, true, remaining);
                        }
                    }
                    else
                    {
                        await tokenBlacklistCacheService.SetTokenStatusAsync(jti, false, NonBlacklistedCacheDuration);
                    }

                    if (isBlacklisted)
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsJsonAsync(new
                        {
                            ErrorCode = "TOKEN_BLACKLISTED",
                            ErrorMessage = "This token has been revoked"
                        });
                        return;
                    }
                }
            }
        }

        await _next(context);
    }
}
