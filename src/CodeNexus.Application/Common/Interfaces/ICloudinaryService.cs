using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Application.Common.Interfaces
{
    public interface ICloudinaryService
    {
        Task<string?> UploadImageAsync(Stream imageStream, string fileName, string folder);
        Task<string?> UploadFileAsync(Stream file, string fileName, string folder);
        Task<bool> DeleteImageAsync(string publicId);
        Task<bool> DeleteFileAsync(string publicId);
    }
}
