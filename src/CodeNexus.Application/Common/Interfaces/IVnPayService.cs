namespace CodeNexus.Application.Common.Interfaces;

public interface IVnPayService
{
    string CreatePaymentUrl(
        string txnRef,
        decimal amount,
        string orderInfo,
        string ipAddress,
        string? returnUrl = null,
        string? ipnUrl = null);

    bool ValidateSignature(IDictionary<string, string> parameters);
}
