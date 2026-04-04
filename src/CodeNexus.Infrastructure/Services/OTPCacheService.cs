using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Domain.Enums;
using StackExchange.Redis;

namespace CodeNexus.Infrastructure.Services;

public class OTPCacheService : IOTPCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IOTPService _otpService;
    private string _lastGeneratedOtp = string.Empty;

    private const int OtpExpirationMinutes = 5;
    private const int ResendRateLimitMinutes = 1;
    private const int MaxResendPerHour = 5;

    public OTPCacheService(IConnectionMultiplexer redis, IOTPService otpService)
    {
        _redis = redis;
        _otpService = otpService;
    }

    public async Task<Result> GenerateAndStoreOtpAsync(
        string email,
        OtpPurpose purpose,
        string? additionalData,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_redis.IsConnected)
            {
                await Task.Delay(1000, cancellationToken);
            }

            if (!_redis.IsConnected)
            {
                return Result.Failure("CACHE_ERROR", "Cache operation failed.");
            }

            var db = _redis.GetDatabase();

            var resendKey = $"otp:resend:{email}";
            var lastResendTime = await db.StringGetAsync(resendKey);

            if (lastResendTime.HasValue)
            {
                return Result.Failure("OTP_RATE_LIMITED", "Please wait 1 minute before requesting a new OTP");
            }

            var resendCountKey = $"otp:resend:count:{email}";
            var resendCountStr = await db.StringGetAsync(resendCountKey);
            var resendCount = resendCountStr.HasValue && int.TryParse(resendCountStr, out var count) ? count : 0;

            if (resendCount >= MaxResendPerHour)
            {
                return Result.Failure("RESEND_RATE_LIMITED", "Too many resend requests. Please try again later");
            }

            var otp = _otpService.GenerateOtp();
            _lastGeneratedOtp = otp;
            var otpHash = _otpService.HashOtp(otp);

            var otpExpiration = TimeSpan.FromMinutes(OtpExpirationMinutes);
            var otpKey = $"otp:{email}";
            await db.StringSetAsync(otpKey, otpHash, otpExpiration);

            var purposeKey = $"otp:purpose:{email}";
            await db.StringSetAsync(purposeKey, purpose.ToString(), otpExpiration);

            if (!string.IsNullOrEmpty(additionalData))
            {
                var dataKey = $"otp:data:{email}";
                await db.StringSetAsync(dataKey, additionalData, otpExpiration);
            }

            var resendExpiration = TimeSpan.FromMinutes(ResendRateLimitMinutes);
            await db.StringSetAsync(resendKey, DateTime.UtcNow.ToString("O"), resendExpiration);

            var hourlyExpiration = TimeSpan.FromHours(1);
            await db.StringSetAsync(resendCountKey, (resendCount + 1).ToString(), hourlyExpiration);

            return Result.Success();
        }
        catch
        {
            return Result.Failure("CACHE_ERROR", "Cache operation failed.");
        }
    }

    public async Task<Result<(string data, OtpPurpose purpose)>> VerifyOtpAsync(
        string email,
        string otp,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_redis.IsConnected)
                return Result<(string, OtpPurpose)>.Failure("CACHE_ERROR", "Cache operation failed.");

            var db = _redis.GetDatabase();

            var otpKey = $"otp:{email}";
            var storedOtpHash = await db.StringGetAsync(otpKey);

            if (!storedOtpHash.HasValue)
            {
                return Result<(string, OtpPurpose)>.Failure("INVALID_OTP", "Invalid OTP code");
            }

            if (!_otpService.VerifyOtp(otp, storedOtpHash.ToString()))
            {
                return Result<(string, OtpPurpose)>.Failure("INVALID_OTP", "Invalid OTP code");
            }

            var purposeKey = $"otp:purpose:{email}";
            var purposeStr = await db.StringGetAsync(purposeKey);

            if (!purposeStr.HasValue || !Enum.TryParse<OtpPurpose>(purposeStr.ToString(), out var purpose))
            {
                return Result<(string, OtpPurpose)>.Failure("INVALID_PURPOSE", "Invalid OTP purpose");
            }

            var dataKey = $"otp:data:{email}";
            var dataValue = await db.StringGetAsync(dataKey);
            var data = dataValue.HasValue ? dataValue.ToString() : string.Empty;

            return Result<(string, OtpPurpose)>.Success((data, purpose));
        }
        catch
        {
            return Result<(string, OtpPurpose)>.Failure("CACHE_ERROR", "Cache operation failed.");
        }
    }

    public async Task<Result> ResendOtpAsync(
        string email,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_redis.IsConnected)
                return Result.Failure("CACHE_ERROR", "Cache operation failed.");

            var db = _redis.GetDatabase();

            var dataKey = $"otp:data:{email}";
            var data = await db.StringGetAsync(dataKey);

            var purposeKey = $"otp:purpose:{email}";
            var purposeStr = await db.StringGetAsync(purposeKey);

            if (!data.HasValue || !purposeStr.HasValue)
            {
                return Result.Failure("INVALID_EMAIL", "No pending verification found for this email");
            }

            if (!Enum.TryParse<OtpPurpose>(purposeStr.ToString(), out var purpose))
            {
                return Result.Failure("INVALID_PURPOSE", "Invalid OTP purpose");
            }

            return await GenerateAndStoreOtpAsync(email, purpose, null, cancellationToken);
        }
        catch
        {
            return Result.Failure("CACHE_ERROR", "Cache operation failed.");
        }
    }

    public async Task<Result<string>> GetDataAsync(
        string email,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_redis.IsConnected)
                return Result<string>.Failure("CACHE_ERROR", "Cache operation failed.");

            var db = _redis.GetDatabase();
            var dataKey = $"otp:data:{email}";
            var data = await db.StringGetAsync(dataKey);

            if (!data.HasValue)
            {
                return Result<string>.Failure("INVALID_PURPOSE", "Invalid OTP purpose");
            }

            return Result<string>.Success(data.ToString());
        }
        catch
        {
            return Result<string>.Failure("CACHE_ERROR", "Cache operation failed.");
        }
    }

    public async Task DeleteOtpDataAsync(
        string email,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!_redis.IsConnected)
                return;

            var db = _redis.GetDatabase();
            var otpKey = $"otp:{email}";
            var dataKey = $"otp:data:{email}";
            var purposeKey = $"otp:purpose:{email}";

            await db.KeyDeleteAsync(otpKey);
            await db.KeyDeleteAsync(dataKey);
            await db.KeyDeleteAsync(purposeKey);
        }
        catch
        {
        }
    }

    public string GetLastGeneratedOtp()
    {
        return _lastGeneratedOtp;
    }
}
