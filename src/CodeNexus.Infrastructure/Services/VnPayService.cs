using System.Security.Cryptography;
using System.Text;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Infrastructure.Settings;
using Microsoft.Extensions.Options;
using System.Net;

namespace CodeNexus.Infrastructure.Services;

public class VnPayService : IVnPayService
{
    private readonly VnPaySettings _settings;

    public VnPayService(IOptions<VnPaySettings> options)
    {
        _settings = options.Value;
        _settings.TmnCode = GetEnvOrDefault("VNPAY__TMNCODE", "VNPAY_TMNCODE", _settings.TmnCode);
        _settings.HashSecret = GetEnvOrDefault("VNPAY__HASHSECRET", "VNPAY_HASHSECRET", _settings.HashSecret);
        _settings.BaseUrl = GetEnvOrDefault("VNPAY__BASEURL", "VNPAY_BASEURL", _settings.BaseUrl);
        _settings.ReturnUrl = GetEnvOrDefault("VNPAY__RETURNURL", "VNPAY_RETURNURL", _settings.ReturnUrl);
        _settings.FrontendReturnUrl = GetEnvOrDefault("VNPAY__FRONTENDRETURNURL", "VNPAY_FRONTENDRETURNURL", _settings.FrontendReturnUrl);
    }

    public string CreatePaymentUrl(
        string txnRef,
        decimal amount,
        string orderInfo,
        string ipAddress,
        string? returnUrl = null,
        string? ipnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(_settings.TmnCode) || string.IsNullOrWhiteSpace(_settings.HashSecret))
        {
            throw new InvalidOperationException("VNPAY configuration is missing TmnCode or HashSecret.");
        }

        var now = GetVietnamNow();
        var expire = now.AddMinutes(15);

        var data = new SortedDictionary<string, string>
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = _settings.TmnCode,
            ["vnp_Amount"] = ((long)(amount * 100)).ToString(),
            ["vnp_CreateDate"] = now.ToString("yyyyMMddHHmmss"),
            ["vnp_CurrCode"] = "VND",
            ["vnp_IpAddr"] = ipAddress,
            ["vnp_Locale"] = "vn",
            ["vnp_OrderInfo"] = orderInfo,
            ["vnp_OrderType"] = "other",
            ["vnp_ReturnUrl"] = string.IsNullOrWhiteSpace(returnUrl) ? _settings.ReturnUrl : returnUrl,
            ["vnp_TxnRef"] = txnRef,
            ["vnp_ExpireDate"] = expire.ToString("yyyyMMddHHmmss")
        };


        var hashData = BuildQueryString(data);
        var hash = ComputeHash(hashData);

        var fullQuery = BuildQueryString(data);
        return $"{_settings.BaseUrl}?{fullQuery}&vnp_SecureHash={hash}";
    }

    public bool ValidateSignature(IDictionary<string, string> parameters)
    {
        if (!parameters.TryGetValue("vnp_SecureHash", out var secureHash) || string.IsNullOrWhiteSpace(secureHash))
        {
            return false;
        }

        var data = parameters
            .Where(kv =>
                kv.Key.StartsWith("vnp_", StringComparison.OrdinalIgnoreCase)
                && kv.Key != "vnp_SecureHash"
                && kv.Key != "vnp_SecureHashType")
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        var hashData = BuildQueryString(data);
        var computed = ComputeHash(hashData);
        return string.Equals(secureHash, computed, StringComparison.OrdinalIgnoreCase);
    }

    private string ComputeHash(string data)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(_settings.HashSecret));
        var bytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static string BuildQueryString(IDictionary<string, string> data)
    {
        var builder = new StringBuilder();
        foreach (var (key, value) in data.OrderBy(x => x.Key))
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            if (builder.Length > 0)
            {
                builder.Append('&');
            }

            builder.Append($"{key}={WebUtility.UrlEncode(value)}");
        }

        return builder.ToString();
    }


    private static DateTime GetVietnamNow()
    {
        try
        {
            var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnTimeZone);
        }
        catch
        {
            return DateTime.UtcNow.AddHours(7);
        }
    }

    private static string GetEnvOrDefault(string primaryEnvKey, string fallbackEnvKey, string currentValue)
    {
        var envValue = Environment.GetEnvironmentVariable(primaryEnvKey);
        if (!string.IsNullOrWhiteSpace(envValue))
        {
            return envValue.Trim();
        }

        envValue = Environment.GetEnvironmentVariable(fallbackEnvKey);
        if (!string.IsNullOrWhiteSpace(envValue))
        {
            return envValue.Trim();
        }

        return currentValue?.Trim() ?? string.Empty;
    }
}
