namespace CodeNexus.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendOtpEmailAsync(string email, string otp, CancellationToken cancellationToken = default);
}
