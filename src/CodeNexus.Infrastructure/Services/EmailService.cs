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
			using var smtpClient = CreateSmtpClient();

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
			using var smtpClient = CreateSmtpClient();

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

	// -------------------------------------------------------------------------
	// SMTP CLIENT
	// -------------------------------------------------------------------------

	private SmtpClient CreateSmtpClient() => new(_settings.SmtpHost, _settings.SmtpPort)
	{
		Credentials = new NetworkCredential(_settings.SenderEmail, _settings.SenderPassword),
		EnableSsl = _settings.EnableSsl,
		DeliveryMethod = SmtpDeliveryMethod.Network
	};

	// -------------------------------------------------------------------------
	// OTP TEMPLATE
	// -------------------------------------------------------------------------

	private static string GetOtpEmailTemplate(string otp)
	{
		var safeOtp = WebUtility.HtmlEncode(otp);

		return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Email Verification — CodeNexus</title>
</head>
<body style=""margin:0;padding:0;background-color:#f4f4f4;font-family:'Courier New',Courier,monospace;"">
    <table role=""presentation"" style=""width:100%;border-collapse:collapse;"">
        <tr>
            <td align=""center"" style=""padding:36px 16px;"">
                <table role=""presentation"" style=""width:100%;max-width:600px;border-collapse:collapse;background:#ffffff;border:1px solid #000;"">

                    <!-- Header -->
                    <tr>
                        <td style=""background:#000;padding:20px 28px;"">
                            <table role=""presentation"" style=""width:100%;border-collapse:collapse;"">
                                <tr>
                                    <td>{GetBrandLogoHtml()}</td>
                                    <td align=""right"" style=""font-size:11px;letter-spacing:1px;color:#555;"">SEC // OTP</td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <!-- Meta bar -->
                    <tr>
                        <td style=""border-bottom:1px solid #000;padding:8px 28px;"">
                            <table role=""presentation"" style=""width:100%;border-collapse:collapse;"">
                                <tr>
                                    <td style=""font-size:10px;letter-spacing:1.5px;color:#888;text-transform:uppercase;"">From: CodeNexus</td>
                                    <td align=""right"" style=""font-size:10px;letter-spacing:1.5px;color:#888;text-transform:uppercase;"">Type: Verification</td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <!-- Body -->
                    <tr>
                        <td style=""padding:32px 28px;"">
                            <p style=""margin:0 0 4px 0;font-size:10px;letter-spacing:2px;color:#888;text-transform:uppercase;"">— Security Check</p>
                            <h2 style=""margin:0 0 24px 0;font-size:22px;font-weight:700;color:#000;letter-spacing:-0.5px;line-height:1.2;"">Email Verification</h2>
                            <p style=""margin:0 0 28px 0;color:#444;font-size:13px;line-height:1.8;font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;"">
                                Thanks for signing up. Enter the code below to complete your registration.
                                Valid for <strong style=""color:#000;"">5 minutes</strong>.
                            </p>

                            <!-- OTP Box -->
                            <table role=""presentation"" style=""width:100%;border-collapse:collapse;margin:0 0 28px 0;"">
                                <tr>
                                    <td style=""border:1px solid #000;padding:24px;text-align:center;"">
                                        <p style=""margin:0 0 8px 0;font-size:10px;letter-spacing:2px;color:#888;text-transform:uppercase;"">Verification Code</p>
                                        <p style=""margin:0;font-size:42px;font-weight:700;color:#000;letter-spacing:14px;font-variant-numeric:tabular-nums;"">{safeOtp}</p>
                                    </td>
                                </tr>
                            </table>

                            <!-- Warning notice -->
                            <table role=""presentation"" style=""width:100%;border-collapse:collapse;margin:0 0 24px 0;"">
                                <tr>
                                    <td style=""border-left:2px solid #000;background:#f9f9f9;padding:12px 16px;"">
                                        <p style=""margin:0;font-size:12px;color:#333;line-height:1.7;font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;"">
                                            <strong>NOTICE:</strong> Never share this code. CodeNexus support will never ask for your OTP.
                                        </p>
                                    </td>
                                </tr>
                            </table>

                            <p style=""margin:0;color:#999;font-size:12px;line-height:1.7;font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;"">
                                Didn't request this? Ignore this email — no action needed.
                            </p>
                        </td>
                    </tr>

                    {GetCommonFooterHtml()}

                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
	}

	// -------------------------------------------------------------------------
	// NOTIFICATION TEMPLATE
	// -------------------------------------------------------------------------

	private static string GetNotificationEmailTemplate(string subject, string message)
	{
		var safeSubject = WebUtility.HtmlEncode(subject);
		var safeMessage = WebUtility.HtmlEncode(message).Replace("\n", "<br />");

		return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{safeSubject} — CodeNexus</title>
</head>
<body style=""margin:0;padding:0;background-color:#f4f4f4;font-family:'Courier New',Courier,monospace;"">
    <table role=""presentation"" style=""width:100%;border-collapse:collapse;"">
        <tr>
            <td align=""center"" style=""padding:36px 16px;"">
                <table role=""presentation"" style=""width:100%;max-width:600px;border-collapse:collapse;background:#ffffff;border:1px solid #000;"">

                    <!-- Header -->
                    <tr>
                        <td style=""background:#000;padding:20px 28px;"">
                            <table role=""presentation"" style=""width:100%;border-collapse:collapse;"">
                                <tr>
                                    <td>{GetBrandLogoHtml()}</td>
                                    <td align=""right"" style=""font-size:11px;letter-spacing:1px;color:#555;"">SYS // NOTIFY</td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <!-- Meta bar -->
                    <tr>
                        <td style=""border-bottom:1px solid #000;padding:8px 28px;"">
                            <table role=""presentation"" style=""width:100%;border-collapse:collapse;"">
                                <tr>
                                    <td style=""font-size:10px;letter-spacing:1.5px;color:#888;text-transform:uppercase;"">From: CodeNexus</td>
                                    <td align=""right"" style=""font-size:10px;letter-spacing:1.5px;color:#888;text-transform:uppercase;"">Type: Alert</td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <!-- Body -->
                    <tr>
                        <td style=""padding:32px 28px;"">
                            <p style=""margin:0 0 4px 0;font-size:10px;letter-spacing:2px;color:#888;text-transform:uppercase;"">— Notification</p>
                            <h2 style=""margin:0 0 24px 0;font-size:22px;font-weight:700;color:#000;letter-spacing:-0.5px;line-height:1.2;"">{safeSubject}</h2>

                            <!-- Message block -->
                            <table role=""presentation"" style=""width:100%;border-collapse:collapse;margin:0 0 28px 0;"">
                                <tr>
                                    <td style=""border-left:2px solid #000;background:#f9f9f9;padding:16px 18px;"">
                                        <p style=""margin:0;color:#222;font-size:14px;line-height:1.8;font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;"">{safeMessage}</p>
                                    </td>
                                </tr>
                            </table>

                            <!-- CTA Button -->
                            <table role=""presentation"" style=""border-collapse:collapse;margin:0 0 28px 0;"">
                                <tr>
                                    <td>
                                        <a href=""https://codenexus-sep.vercel.app/""
                                           style=""display:inline-block;background:#000;color:#fff;font-size:12px;font-weight:700;padding:12px 24px;text-decoration:none;letter-spacing:2px;text-transform:uppercase;font-family:'Courier New',Courier,monospace;"">
                                            Open CodeNexus →
                                        </a>
                                    </td>
                                </tr>
                            </table>

                            <p style=""margin:0;color:#999;font-size:12px;line-height:1.7;font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;"">
                                This message was sent automatically by the CodeNexus notification system.
                                You are receiving this because you are enrolled in an active learning path.
                            </p>
                        </td>
                    </tr>

                    {GetCommonFooterHtml()}

                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
	}

	// -------------------------------------------------------------------------
	// SHARED PARTIALS
	// -------------------------------------------------------------------------

	private static string GetBrandLogoHtml()
	{
		return @"<div style=""display: inline-flex; align-items: center; font-family: 'Courier New', Courier, monospace; text-decoration: none; user-select: none;"">
    <span style=""color: #4d9eff; font-weight: bold; font-size: 20px;"">&gt;_</span>
    <span style=""font-size: 20px; font-weight: bold; color: #ffffff; margin-left: 6px;"">CodeNexus</span>
    <span style=""display: inline-block; width: 10px; height: 20px; background-color: #4d9eff; margin-left: 4px;"">&nbsp;</span>
</div>";
	}

	private static string GetCommonFooterHtml()
	{
		return @"<tr>
    <td style=""border-top: 1px solid #000; padding: 14px 28px; background: #f9f9f9;"">
        <table role=""presentation"" style=""width: 100%; border-collapse: collapse; font-family: 'Courier New', Courier, monospace;"">
            <tr>
                <td style=""font-size: 10px; letter-spacing: 1.2px; color: #888; text-transform: uppercase;"">© 2026 CodeNexus • Build with consistency.</td>
                <td align=""right"" style=""font-size: 10px; letter-spacing: 1.2px; color: #bbb; text-transform: uppercase;"">Automated · Do not reply</td>
            </tr>
        </table>
    </td>
</tr>";
	}
}
