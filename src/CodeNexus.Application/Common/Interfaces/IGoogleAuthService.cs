namespace CodeNexus.Application.Common.Interfaces;

public interface IGoogleAuthService
{
    Task<GoogleUserInfo?> ValidateIdTokenAsync(string idToken, CancellationToken cancellationToken);
}

public record GoogleUserInfo(string Email, string? GivenName, string? FamilyName, string? Name);
