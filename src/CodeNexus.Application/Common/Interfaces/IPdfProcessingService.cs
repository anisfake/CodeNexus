using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CodeNexus.Application.Common.Interfaces
{
    public interface IPdfProcessingService
    {
        Task<PdfProcessingResult> ProcessPdfAsync(Stream pdfStream, string userId);
        Task<int> GetPageCountAsync(Stream pdfStream);
    }

    public class PdfProcessingResult
    {
        public int TotalPages { get; set; }
        public List<PdfPageData> Pages { get; set; } = new List<PdfPageData>();
    }

    public class PdfPageData
    {
        public int PageNumber { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string? ExtractedText { get; set; }
    }
}
