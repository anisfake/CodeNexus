using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Infrastructure.Settings;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;

namespace CodeNexus.Infrastructure.Services;

public class GoogleAuthService : IGoogleAuthService
{
    private readonly GoogleAuthSettings _settings;

    public GoogleAuthService(IOptions<GoogleAuthSettings> settings)
    {
        _settings = settings.Value;
    }

    public async Task<GoogleUserInfo?> ValidateIdTokenAsync(string idToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.ClientId))
            return null;

        try
        {
            GoogleJsonWebSignature.Payload payload;
            try
            {
                payload = await GoogleJsonWebSignature.ValidateAsync(
                    idToken,
                    new GoogleJsonWebSignature.ValidationSettings
                    {
                        Audience = new[] { _settings.ClientId }
                    });
            }
            catch
            {
                payload = await GoogleJsonWebSignature.ValidateAsync(idToken);

                var aud = payload.Audience as string;

                if (!string.Equals(aud, _settings.ClientId, StringComparison.Ordinal))
                {
                    return null;
                }
            }

            if (string.IsNullOrWhiteSpace(payload.Email))
                return null;

            return new GoogleUserInfo(payload.Email, payload.GivenName, payload.FamilyName, payload.Name);
        }
        catch
        {
            return null;
        }
    }
}
