using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Application.Common.Interfaces
{
    public interface ICloudinaryService
    {
        Task<string> UploadImagesAsync(Stream imageStream, string fileName, string folder);

    }
}
