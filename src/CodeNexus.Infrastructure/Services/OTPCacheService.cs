using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Application.Common.Models;
using CodeNexus.Domain.Enums;
using Microsoft.Extensions.Caching.Distributed;

namespace CodeNexus.Infrastructure.Services;

public class OTPCacheService : IOTPCacheService
{
    private readonly IDistributedCache _cache;
    private readonly IOTPService _otpService;
    private string _lastGeneratedOtp = string.Empty;

    private const int OtpExpirationMinutes = 5;
    private const int ResendRateLimitMinutes = 1;
    private const int MaxResendPerHour = 5;

    public OTPCacheService(IDistributedCache cache, IOTPService otpService)
    {
        _cache = cache;
        _otpService = otpService;
    }

    public async Task<Result> GenerateAndStoreOtpAsync(
        string email,
        OtpPurpose purpose,
        string? additionalData,
        CancellationToken cancellationToken)
    {
        var resendKey = $"otp:resend:{email}";
        var lastResendTime = await _cache.GetStringAsync(resendKey, cancellationToken);

        if (lastResendTime != null)
        {
            return Result.Failure("OTP_RATE_LIMITED", "Please wait 1 minute before requesting a new OTP");
        }

        var resendCountKey = $"otp:resend:count:{email}";
        var resendCountStr = await _cache.GetStringAsync(resendCountKey, cancellationToken);
        var resendCount = int.TryParse(resendCountStr, out var count) ? count : 0;

        if (resendCount >= MaxResendPerHour)
        {
            return Result.Failure("RESEND_RATE_LIMITED", "Too many resend requests. Please try again later");
        }

        var otp = _otpService.GenerateOtp();
        _lastGeneratedOtp = otp;
        var otpHash = _otpService.HashOtp(otp);

        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(OtpExpirationMinutes)
        };

        var otpKey = $"otp:{email}";
        await _cache.SetStringAsync(otpKey, otpHash, cacheOptions, cancellationToken);

        var purposeKey = $"otp:purpose:{email}";
        await _cache.SetStringAsync(purposeKey, purpose.ToString(), cacheOptions, cancellationToken);

        if (!string.IsNullOrEmpty(additionalData))
        {
            var dataKey = $"otp:data:{email}";
            await _cache.SetStringAsync(dataKey, additionalData, cacheOptions, cancellationToken);
        }

        var resendOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(ResendRateLimitMinutes)
        };
        await _cache.SetStringAsync(resendKey, DateTime.Now.ToString("O"), resendOptions, cancellationToken);

        var hourlyOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
        };
        await _cache.SetStringAsync(resendCountKey, (resendCount + 1).ToString(), hourlyOptions, cancellationToken);

        return Result.Success();
    }

    public async Task<Result<(string data, OtpPurpose purpose)>> VerifyOtpAsync(
        string email,
        string otp,
        CancellationToken cancellationToken)
    {
        var otpKey = $"otp:{email}";
        var storedOtpHash = await _cache.GetStringAsync(otpKey, cancellationToken);

        if (storedOtpHash == null)
        {
            return Result<(string, OtpPurpose)>.Failure("INVALID_OTP", "No pending verification found for this email");
        }

        if (!_otpService.VerifyOtp(otp, storedOtpHash))
        {
            return Result<(string, OtpPurpose)>.Failure("INVALID_OTP", "Invalid OTP code");
        }

        var purposeKey = $"otp:purpose:{email}";
        var purposeStr = await _cache.GetStringAsync(purposeKey, cancellationToken);

        if (purposeStr == null || !Enum.TryParse<OtpPurpose>(purposeStr, out var purpose))
        {
            return Result<(string, OtpPurpose)>.Failure("INVALID_PURPOSE", "OTP purpose not found");
        }

        var dataKey = $"otp:data:{email}";
        var data = await _cache.GetStringAsync(dataKey, cancellationToken) ?? string.Empty;

        return Result<(string, OtpPurpose)>.Success((data, purpose));
    }

    public async Task<Result> ResendOtpAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var dataKey = $"otp:data:{email}";
        var data = await _cache.GetStringAsync(dataKey, cancellationToken);

        var purposeKey = $"otp:purpose:{email}";
        var purposeStr = await _cache.GetStringAsync(purposeKey, cancellationToken);

        if (data == null || purposeStr == null)
        {
            return Result.Failure("INVALID_EMAIL", "No pending verification found for this email");
        }

        if (!Enum.TryParse<OtpPurpose>(purposeStr, out var purpose))
        {
            return Result.Failure("INVALID_PURPOSE", "Invalid OTP purpose");
        }

        return await GenerateAndStoreOtpAsync(email, purpose, null, cancellationToken);
    }

    public async Task<Result<string>> GetDataAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var dataKey = $"otp:data:{email}";
        var data = await _cache.GetStringAsync(dataKey, cancellationToken);

        if (data == null)
        {
            return Result<string>.Failure("INVALID_PURPOSE", "Data not found");
        }

        return Result<string>.Success(data);
    }

    public async Task DeleteOtpDataAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var otpKey = $"otp:{email}";
        var dataKey = $"otp:data:{email}";
        var purposeKey = $"otp:purpose:{email}";

        await _cache.RemoveAsync(otpKey, cancellationToken);
        await _cache.RemoveAsync(dataKey, cancellationToken);
        await _cache.RemoveAsync(purposeKey, cancellationToken);
    }

    public string GetLastGeneratedOtp()
    {
        return _lastGeneratedOtp;
    }
}
