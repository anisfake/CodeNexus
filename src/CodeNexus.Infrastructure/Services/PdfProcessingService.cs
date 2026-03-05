using CodeNexus.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using PDFtoImage;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using PdfPigDocument = UglyToad.PdfPig.PdfDocument;

namespace CodeNexus.Infrastructure.Services
{
    public class PdfProcessingService : IPdfProcessingService
    {
        private readonly ICloudinaryService _cloudinaryService;
        private readonly IOcrService _ocrService;
        private readonly ILogger<PdfProcessingService> _logger;

        public PdfProcessingService(
            ICloudinaryService cloudinaryService,
            IOcrService ocrService,
            ILogger<PdfProcessingService> logger)
        {
            _cloudinaryService = cloudinaryService;
            _ocrService = ocrService;
            _logger = logger;
        }

        public async Task<int> GetPageCountAsync(Stream pdfStream)
        {
            try
            {
                using var memoryStream = new MemoryStream();
                await pdfStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                using var document = PdfPigDocument.Open(memoryStream);
                return document.NumberOfPages;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to get page count: {ex.Message}", ex);
            }
        }

        public async Task<PdfProcessingResult> ProcessPdfAsync(Stream pdfStream, string userId)
        {
            try
            {
                using var memoryStream = new MemoryStream();
                await pdfStream.CopyToAsync(memoryStream);
                var pdfBytes = memoryStream.ToArray();

                var result = new PdfProcessingResult();
                var pages = new List<PdfPageData>();

                memoryStream.Position = 0;
                using (var document = PdfPigDocument.Open(memoryStream))
                {
                    result.TotalPages = document.NumberOfPages;

                    for (int pageIndex = 1; pageIndex <= result.TotalPages; pageIndex++)
                    {
                        var pageData = await ProcessPageAsync(document, pdfBytes, pageIndex, userId);
                        pages.Add(pageData);
                    }
                }

                result.Pages = pages;
                return result;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to process PDF: {ex.Message}", ex);
            }
        }

        private async Task<PdfPageData> ProcessPageAsync(PdfPigDocument document, byte[] pdfBytes, int pageNumber, string userId)
        {
            _logger.LogInformation($"Processing page {pageNumber}...");

            var (imageUrl, renderedImageBytes) = await RenderAndUploadPageAsync(pdfBytes, pageNumber - 1, pageNumber, userId);

            var extractedText = await _ocrService.ExtractTextFromImageAsync(renderedImageBytes);

            _logger.LogInformation($"Page {pageNumber}: Extracted {extractedText?.Length ?? 0} characters via OCR");

            return new PdfPageData
            {
                PageNumber = pageNumber,
                ImageUrl = imageUrl,
                ExtractedText = extractedText
            };
        }

        private async Task<(string imageUrl, byte[] imageBytes)> RenderAndUploadPageAsync(byte[] pdfBytes, int pageIndex, int pageNumber, string userId)
        {
            try
            {
                _logger.LogInformation($"Rendering page {pageNumber} with PDFtoImage (includes text layer)...");

                // Render at 300 DPI for high quality - this renders EVERYTHING including text layer as pixels
                var options = new RenderOptions(Dpi: 300, WithAnnotations: true, WithFormFill: true);
                var skBitmap = Conversion.ToImage(pdfBytes, page: pageIndex, options: options);

                // Convert SKBitmap to JPEG bytes
                using var image = SKImage.FromBitmap(skBitmap);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, 95);
                var imageBytes = data.ToArray();

                // Upload to Cloudinary
                using var jpegStream = new MemoryStream(imageBytes);
                var fileName = $"page_{pageNumber}.jpg";
                var imageUrl = await _cloudinaryService.UploadImageAsync(jpegStream, fileName, $"resources/{userId}/pages");

                _logger.LogInformation($"Page {pageNumber}: Uploaded to {imageUrl} ({imageBytes.Length} bytes)");

                skBitmap.Dispose();

                return (imageUrl, imageBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to render page {pageNumber}");
                throw;
            }
        }
    }
}
