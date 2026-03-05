namespace CodeNexus.Application.Common.Interfaces;

public interface IOcrService
{
    Task<string?> ExtractTextFromImageAsync(byte[] imageBytes);
}
