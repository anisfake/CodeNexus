using CodeNexus.Application.Common.Interfaces;
using Docnet.Core;
using Docnet.Core.Readers;
using Docnet.Core.Models;
using SkiaSharp;
using Tesseract;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace CodeNexus.Infrastructure.Services
{
    public class PdfProcessingService : IPdfProcessingService
    {
        private readonly ICloudinaryService _cloudinaryService;
        private readonly string _tessDataPath;

        public PdfProcessingService(ICloudinaryService cloudinaryService)
        {
            _cloudinaryService = cloudinaryService;

            _tessDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");

            if (!Directory.Exists(_tessDataPath))
            {
                Directory.CreateDirectory(_tessDataPath);
            }
        }

        public async Task<int> GetPageCountAsync(Stream pdfStream)
        {
            try
            {
                using var memoryStream = new MemoryStream();
                await pdfStream.CopyToAsync(memoryStream);
                var pdfBytes = memoryStream.ToArray();

                using var docReader = DocLib.Instance.GetDocReader(pdfBytes, new PageDimensions(1.5));
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

                using (var docReader = DocLib.Instance.GetDocReader(pdfBytes, new PageDimensions(1.5)))
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

            var (imageUrl, imageBytes) = await RenderPageToImageAsync(docReader, pageIndex, pageNumber, userId);

            var extractedText = await ExtractTextFromImageAsync(imageBytes);

            return new PdfPageData
            {
                PageNumber = pageNumber,
                ImageUrl = imageUrl,
                ExtractedText = extractedText
            };
        }

        private async Task<(string imageUrl, byte[] imageBytes)> RenderPageToImageAsync(IDocReader docReader, int pageIndex, int pageNumber, string userId)
        {
            try
            {
                using var pageReader = docReader.GetPageReader(pageIndex);
                var width = pageReader.GetPageWidth();
                var height = pageReader.GetPageHeight();

                if (width <= 0 || height <= 0)
                {
                    throw new InvalidOperationException($"Invalid page dimensions: {width}x{height}");
                }

                const int maxDimension = 4096;
                if (width > maxDimension || height > maxDimension)
                {
                    var scale = Math.Min((double)maxDimension / width, (double)maxDimension / height);
                    width = (int)(width * scale);
                    height = (int)(height * scale);
                }

                var rawBytes = pageReader.GetImage();
                var expectedSize = width * height * 4;

                if (rawBytes.Length < expectedSize)
                {
                    throw new InvalidOperationException(
                        $"Image data size insufficient. Expected: {expectedSize}, Actual: {rawBytes.Length}, " +
                        $"Width: {width}, Height: {height}");
                }

                using var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
                if (bitmap.GetPixels() == IntPtr.Zero)
                {
                    throw new InvalidOperationException($"Failed to allocate bitmap memory for {width}x{height}");
                }

                var pixelPtr = bitmap.GetPixels();
                var bytesToCopy = Math.Min(rawBytes.Length, expectedSize);

                System.Runtime.InteropServices.Marshal.Copy(rawBytes, 0, pixelPtr, bytesToCopy);

                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 90);

                var imageBytes = data.ToArray();

                using var imageStream = new MemoryStream(imageBytes);
                var fileName = $"page_{pageNumber}.png";
                var imageUrl = await _cloudinaryService.UploadImageAsync(imageStream, fileName, $"resources/{userId}/pages");

                return (imageUrl, imageBytes);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to render page {pageNumber}: {ex.Message}", ex);
            }
        }

        private async Task<string?> ExtractTextFromImageAsync(byte[] imageBytes)
        {
            try
            {
                var engDataPath = Path.Combine(_tessDataPath, "eng.traineddata");
                var vieDataPath = Path.Combine(_tessDataPath, "vie.traineddata");

                var languages = new List<string>();

                if (File.Exists(vieDataPath))
                {
                    languages.Add("vie");
                }

                if (File.Exists(engDataPath))
                {
                    languages.Add("eng");
                }

                if (languages.Count == 0)
                {
                    Console.WriteLine($"Tesseract data not found at {_tessDataPath}. OCR will be skipped.");
                    return null;
                }

                var languageString = string.Join("+", languages);

                using var engine = new TesseractEngine(_tessDataPath, languageString, EngineMode.Default);

                using var img = Pix.LoadFromMemory(imageBytes);
                using var page = engine.Process(img);

                var text = page.GetText();

                return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OCR failed: {ex.Message}");
                return null;
            }
        }
    }
}
