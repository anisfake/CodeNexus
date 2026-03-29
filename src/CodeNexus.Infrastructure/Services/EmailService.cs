using System.Net;
using System.Net.Mail;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Infrastructure.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeNexus.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendNotificationEmailAsync(string email, string subject, string message, CancellationToken cancellationToken = default)
    {
        try
        {
            using var smtpClient = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
            {
                Credentials = new NetworkCredential(_settings.SenderEmail, _settings.SenderPassword),
                EnableSsl = _settings.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_settings.SenderEmail, _settings.SenderName),
                Subject = $"CodeNexus - {subject}",
                Body = GetNotificationEmailTemplate(subject, message),
                IsBodyHtml = true
            };
            mailMessage.To.Add(email);

            await smtpClient.SendMailAsync(mailMessage, cancellationToken);
            _logger.LogInformation("Notification email sent successfully to {Email}", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification email to {Email}", email);
            throw;
        }
    }
    public async Task SendOtpEmailAsync(string email, string otp, CancellationToken cancellationToken = default)
    {
        try
        {
            using var smtpClient = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
            {
                Credentials = new NetworkCredential(_settings.SenderEmail, _settings.SenderPassword),
                EnableSsl = _settings.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_settings.SenderEmail, _settings.SenderName),
                Subject = "CodeNexus - Email Verification Code",
                Body = GetOtpEmailTemplate(otp),
                IsBodyHtml = true
            };
            mailMessage.To.Add(email);

            await smtpClient.SendMailAsync(mailMessage, cancellationToken);
            _logger.LogInformation("OTP email sent successfully to {Email}", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send OTP email to {Email}", email);
            throw;
        }
    }

    private static string GetOtpEmailTemplate(string otp)
    {
        return $@"
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Email Verification</title>
</head>
<body style=""margin: 0; padding: 0; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f4f4f4;"">
    <table role=""presentation"" style=""width: 100%; border-collapse: collapse;"">
        <tr>
            <td align=""center"" style=""padding: 40px 0;"">
                <table role=""presentation"" style=""width: 600px; border-collapse: collapse; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1);"">
                    <!-- Header -->
                    <tr>
                        <td style=""padding: 40px 40px 20px 40px; text-align: center; background-color: #4F46E5; border-radius: 8px 8px 0 0;"">
                            <h1 style=""margin: 0; color: #ffffff; font-size: 28px; font-weight: 600;"">CodeNexus</h1>
                        </td>
                    </tr>
                    <!-- Content -->
                    <tr>
                        <td style=""padding: 40px;"">
                            <h2 style=""margin: 0 0 20px 0; color: #333333; font-size: 24px; font-weight: 600;"">Email Verification</h2>
                            <p style=""margin: 0 0 20px 0; color: #666666; font-size: 16px; line-height: 1.6;"">
                                Thank you for registering with CodeNexus. Please use the following verification code to complete your registration:
                            </p>
                            <div style=""text-align: center; margin: 30px 0;"">
                                <span style=""display: inline-block; padding: 15px 40px; background-color: #f0f0f0; border-radius: 8px; font-size: 32px; font-weight: bold; letter-spacing: 8px; color: #4F46E5;"">{otp}</span>
                            </div>
                            <p style=""margin: 0 0 10px 0; color: #666666; font-size: 14px; line-height: 1.6;"">
                                This code will expire in <strong>5 minutes</strong>.
                            </p>
                            <p style=""margin: 0; color: #999999; font-size: 14px; line-height: 1.6;"">
                                If you did not request this verification code, please ignore this email.
                            </p>
                        </td>
                    </tr>
                    <!-- Footer -->
                    <tr>
                        <td style=""padding: 20px 40px; background-color: #f9f9f9; border-radius: 0 0 8px 8px; text-align: center;"">
                            <p style=""margin: 0; color: #999999; font-size: 12px;"">
                                &copy; 2026 CodeNexus. All rights reserved.
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    private static string GetNotificationEmailTemplate(string subject, string message)
    {
        return $@"
<!DOCTYPE html>
<html lang=""en""><head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0""><title>{subject}</title></head>
<body style=""margin:0;padding:0;font-family:'Segoe UI',Tahoma,Verdana,sans-serif;background:#f4f4f4;"">
    <table role=""presentation"" style=""width:100%;border-collapse:collapse;"">
        <tr><td align=""center"" style=""padding:40px 0;"">
            <table role=""presentation"" style=""width:600px;border-collapse:collapse;background:#fff;border-radius:8px;"">
                <tr><td style=""padding:24px;background:#4F46E5;border-radius:8px 8px 0 0;color:#fff;font-size:24px;font-weight:600;text-align:center;"">CodeNexus</td></tr>
                <tr><td style=""padding:32px;"">
                    <h2 style=""margin:0 0 16px 0;color:#333;"">{subject}</h2>
                    <p style=""margin:0;color:#666;line-height:1.6;font-size:15px;"">{message}</p>
                </td></tr>
            </table>
        </td></tr>
    </table>
</body>
</html>";
    }
}
