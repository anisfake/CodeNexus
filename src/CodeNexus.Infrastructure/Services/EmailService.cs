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
<body style=""margin:0;padding:0;background-color:#E6F1FB;font-family:'Courier New',Courier,monospace;"">
    <table role=""presentation"" style=""width:100%;border-collapse:collapse;"">
        <tr>
            <td align=""center"" style=""padding:36px 16px;"">
                <table role=""presentation"" style=""width:100%;max-width:600px;border-collapse:collapse;background:#ffffff;border:1.5px solid #0969da;"">

                    <!-- Header -->
                    <tr>
                        <td style=""background:#ffffff;padding:0 28px;border-bottom:1.5px solid #0969da;"">
                            <table role=""presentation"" style=""width:100%;border-collapse:collapse;"">
                                <tr>
                                    <td style=""padding:18px 0;"">
                                        {GetBrandLogoHtml()}
                                    </td>
                                    <td align=""right"" style=""padding:0;"">
                                        <div style=""background:#0969da;padding:0 18px;display:inline-block;"">
                                            <span style=""font-size:10px;letter-spacing:2px;color:#ffffff;text-transform:uppercase;white-space:nowrap;line-height:60px;display:inline-block;"">SEC // OTP</span>
                                        </div>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <!-- Meta bar -->
                    <tr>
                        <td style=""background:#E6F1FB;border-bottom:1px solid #B5D4F4;padding:7px 28px;"">
                            <table role=""presentation"" style=""width:100%;border-collapse:collapse;"">
                                <tr>
                                    <td style=""font-size:10px;letter-spacing:1px;color:#185FA5;text-transform:uppercase;"">noreply@codenexus.app</td>
                                    <td align=""right"" style=""font-size:10px;letter-spacing:1px;color:#378ADD;text-transform:uppercase;"">Type: Verification</td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <!-- Body -->
                    <tr>
                        <td style=""padding:32px 28px 28px;"">
                            <p style=""margin:0 0 6px 0;font-size:10px;letter-spacing:2px;color:#378ADD;text-transform:uppercase;"">— Security Check</p>
                            <h2 style=""margin:0 0 20px 0;font-size:24px;font-weight:700;color:#24292f;line-height:1.2;"">Email Verification</h2>
                            <p style=""margin:0 0 28px 0;color:#444444;font-size:13px;line-height:1.8;font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;"">
                                Thanks for signing up. Enter the code below to complete your registration.
                                Valid for <strong style=""color:#24292f;"">5 minutes</strong>.
                            </p>

                            <!-- OTP Box -->
                            <table role=""presentation"" style=""width:100%;border-collapse:collapse;margin:0 0 24px 0;"">
                                <tr>
                                    <td style=""border:1.5px solid #0969da;"">
                                        <table role=""presentation"" style=""width:100%;border-collapse:collapse;"">
                                            <tr>
                                                <td style=""background:#0969da;padding:8px 20px;"">
                                                    <span style=""font-size:10px;letter-spacing:2px;color:#ffffff;text-transform:uppercase;"">Verification Code</span>
                                                </td>
                                            </tr>
                                            <tr>
                                                <td style=""padding:24px;text-align:center;background:#ffffff;"">
                                                    <span style=""font-size:44px;font-weight:700;color:#24292f;letter-spacing:14px;font-variant-numeric:tabular-nums;font-family:'Courier New',Courier,monospace;"">{safeOtp}</span>
                                                </td>
                                            </tr>
                                        </table>
                                    </td>
                                </tr>
                            </table>

                            <!-- Warning notice -->
                            <table role=""presentation"" style=""width:100%;border-collapse:collapse;margin:0 0 24px 0;"">
                                <tr>
                                    <td style=""border-left:3px solid #0969da;background:#E6F1FB;padding:12px 16px;"">
                                        <p style=""margin:0;font-size:12px;color:#0C447C;line-height:1.7;font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;"">
                                            <strong>NOTICE:</strong> Never share this code. CodeNexus support will never ask for your OTP.
                                        </p>
                                    </td>
                                </tr>
                            </table>

                            <p style=""margin:0;color:#888888;font-size:12px;line-height:1.7;font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;"">
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
<body style=""margin:0;padding:0;background-color:#E6F1FB;font-family:'Courier New',Courier,monospace;"">
    <table role=""presentation"" style=""width:100%;border-collapse:collapse;"">
        <tr>
            <td align=""center"" style=""padding:36px 16px;"">
                <table role=""presentation"" style=""width:100%;max-width:600px;border-collapse:collapse;background:#ffffff;border:1.5px solid #0969da;"">

                    <!-- Header -->
                    <tr>
                        <td style=""background:#ffffff;padding:0 28px;border-bottom:1.5px solid #0969da;"">
                            <table role=""presentation"" style=""width:100%;border-collapse:collapse;"">
                                <tr>
                                    <td style=""padding:18px 0;"">
                                        {GetBrandLogoHtml()}
                                    </td>
                                    <td align=""right"" style=""padding:0;"">
                                        <div style=""background:#0969da;padding:0 18px;display:inline-block;"">
                                            <span style=""font-size:10px;letter-spacing:2px;color:#ffffff;text-transform:uppercase;white-space:nowrap;line-height:60px;display:inline-block;"">SYS // NOTIFY</span>
                                        </div>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <!-- Meta bar -->
                    <tr>
                        <td style=""background:#E6F1FB;border-bottom:1px solid #B5D4F4;padding:7px 28px;"">
                            <table role=""presentation"" style=""width:100%;border-collapse:collapse;"">
                                <tr>
                                    <td style=""font-size:10px;letter-spacing:1px;color:#185FA5;text-transform:uppercase;"">noreply@codenexus.app</td>
                                    <td align=""right"" style=""font-size:10px;letter-spacing:1px;color:#378ADD;text-transform:uppercase;"">Type: Alert</td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <!-- Body -->
                    <tr>
                        <td style=""padding:32px 28px 28px;"">
                            <p style=""margin:0 0 6px 0;font-size:10px;letter-spacing:2px;color:#378ADD;text-transform:uppercase;"">— Notification</p>
                            <h2 style=""margin:0 0 20px 0;font-size:24px;font-weight:700;color:#24292f;line-height:1.2;"">{safeSubject}</h2>

                            <!-- Message block -->
                            <table role=""presentation"" style=""width:100%;border-collapse:collapse;margin:0 0 20px 0;"">
                                <tr>
                                    <td style=""border-left:3px solid #0969da;background:#E6F1FB;padding:16px 18px;"">
                                        <p style=""margin:0;color:#0C447C;font-size:14px;line-height:1.8;font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;"">{safeMessage}</p>
                                    </td>
                                </tr>
                            </table>

                            <!-- Impact / Action table -->
                            <table role=""presentation"" style=""width:100%;border-collapse:collapse;margin:0 0 28px 0;font-family:'Segoe UI',Tahoma,Geneva,Verdana,sans-serif;"">
                                <tr>
                                    <td style=""padding:10px 0;border-top:1px solid #B5D4F4;font-size:10px;letter-spacing:1.5px;color:#378ADD;text-transform:uppercase;width:110px;vertical-align:top;"">Impact</td>
                                    <td style=""padding:10px 0;border-top:1px solid #B5D4F4;font-size:13px;color:#333333;line-height:1.6;"">Learning progress may be disrupted. Related tasks will continue to accumulate.</td>
                                </tr>
                                <tr>
                                    <td style=""padding:10px 0;border-top:1px solid #B5D4F4;font-size:10px;letter-spacing:1.5px;color:#378ADD;text-transform:uppercase;vertical-align:top;"">Action</td>
                                    <td style=""padding:10px 0;border-top:1px solid #B5D4F4;font-size:13px;color:#333333;line-height:1.6;"">Open CodeNexus → review your timeline → complete pending items.</td>
                                </tr>
                            </table>

                            <!-- CTA Button -->
                            <table role=""presentation"" style=""border-collapse:collapse;"">
                                <tr>
                                    <td>
                                        <a href=""https://codenexus-sep.vercel.app/""
                                           style=""display:inline-block;background:#0969da;color:#ffffff;font-size:11px;font-weight:700;padding:12px 24px;text-decoration:none;letter-spacing:2px;text-transform:uppercase;font-family:'Courier New',Courier,monospace;"">
                                            Open CodeNexus →
                                        </a>
                                    </td>
                                </tr>
                            </table>
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
    <span style=""color: #0969da; font-weight: bold; font-size: 20px;"">&gt;_</span>
    <span style=""font-size: 20px; font-weight: bold; color: #24292f; margin-left: 6px;"">CodeNexus</span>
    <span style=""display: inline-block; width: 10px; height: 20px; background-color: #0969da; margin-left: 4px;"">&nbsp;</span>
</div>";
	}

	private static string GetCommonFooterHtml()
	{
		return @"<tr>
    <td style=""background:#0969da;padding:14px 28px;"">
        <table role=""presentation"" style=""width:100%;border-collapse:collapse;font-family:'Courier New',Courier,monospace;"">
            <tr>
                <td style=""font-size:10px;letter-spacing:1px;color:#B5D4F4;text-transform:uppercase;"">© 2026 CodeNexus · Build with consistency.</td>
                <td align=""right"" style=""font-size:10px;letter-spacing:1px;color:#85B7EB;text-transform:uppercase;"">Automated · Do not reply</td>
            </tr>
        </table>
    </td>
</tr>";
	}
}