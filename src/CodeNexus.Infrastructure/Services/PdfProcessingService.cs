using CodeNexus.Application.Common.Interfaces;
using Docnet.Core;
using Docnet.Core.Readers;
using Docnet.Core.Models;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CodeNexus.Infrastructure.Services
{
    public class PdfProcessingService : IPdfProcessingService
    {
        private readonly ICloudinaryService _cloudinaryService;

        public PdfProcessingService(ICloudinaryService cloudinaryService)
        {
            _cloudinaryService = cloudinaryService;
        }

        public async Task<int> GetPageCountAsync(Stream pdfStream)
        {
            try
            {
                using var memoryStream = new MemoryStream();
                await pdfStream.CopyToAsync(memoryStream);
                var pdfBytes = memoryStream.ToArray();

                using var docReader = DocLib.Instance.GetDocReader(pdfBytes, new PageDimensions());
                return docReader.GetPageCount();
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

                using (var docReader = DocLib.Instance.GetDocReader(pdfBytes, new PageDimensions()))
                {
                    result.TotalPages = docReader.GetPageCount();

                    for (int pageIndex = 0; pageIndex < result.TotalPages; pageIndex++)
                    {
                        var pageData = await ProcessPageAsync(docReader, pdfBytes, pageIndex, userId);
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

        private async Task<PdfPageData> ProcessPageAsync(IDocReader docReader, byte[] pdfBytes, int pageIndex, string userId)
        {
            var pageNumber = pageIndex + 1;

            var imageUrl = await RenderPageToImageAsync(docReader, pageIndex, pageNumber, userId);

            var extractedText = ExtractTextFromPage(pdfBytes, pageIndex);

            return new PdfPageData
            {
                PageNumber = pageNumber,
                ImageUrl = imageUrl,
                ExtractedText = extractedText
            };
        }

        private async Task<string> RenderPageToImageAsync(IDocReader docReader, int pageIndex, int pageNumber, string userId)
        {
            try
            {
                using var pageReader = docReader.GetPageReader(pageIndex);
                var width = pageReader.GetPageWidth();
                var height = pageReader.GetPageHeight();
                var rawBytes = pageReader.GetImage();

                var expectedSize = width * height * 4;

                if (rawBytes.Length < expectedSize)
                {
                    throw new InvalidOperationException(
                        $"Image data size insufficient. Expected: {expectedSize}, Actual: {rawBytes.Length}, " +
                        $"Width: {width}, Height: {height}");
                }

                var bytesToCopy = Math.Min(rawBytes.Length, expectedSize);

                using var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
                var pixelPtr = bitmap.GetPixels();

                System.Runtime.InteropServices.Marshal.Copy(rawBytes, 0, pixelPtr, bytesToCopy);

                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 90);
                using var imageStream = data.AsStream();

                var fileName = $"page_{pageNumber}.png";
                var imageUrl = await _cloudinaryService.UploadImageAsync(imageStream, fileName, $"resources/{userId}/pages");

                return imageUrl;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to render page {pageNumber}: {ex.Message}", ex);
            }
        }

        private string? ExtractTextFromPage(byte[] pdfBytes, int pageIndex)
        {
            try
            {
                using var pdfStream = new MemoryStream(pdfBytes);
                using var pdfReader = new PdfReader(pdfStream);
                using var pdfDocument = new PdfDocument(pdfReader);

                var page = pdfDocument.GetPage(pageIndex + 1);
                var strategy = new SimpleTextExtractionStrategy();
                var text = PdfTextExtractor.GetTextFromPage(page, strategy);

                return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to extract text from page {pageIndex + 1}: {ex.Message}");
                return null;
            }
        }
    }
}
