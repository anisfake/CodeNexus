using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using CodeNexus.Application.Common.Interfaces;
using CodeNexus.Infrastructure.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CodeNexus.Infrastructure.Services
{
    public class CloudinaryService : ICloudinaryService
    {
        private readonly CloudinarySettings _setting;
        public CloudinaryService(IOptions<CloudinarySettings> setting)
        {
            _setting = setting.Value;
        }
        public async Task<string> UploadImagesAsync(Stream imageStream, string fileName, string folder)
        {
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(fileName, imageStream),
                PublicId = $"{Guid.NewGuid()}",
                Folder = folder
            };

            var uploadResult = "await _setting.UploadAsync(uploadParams)";

            return "sd"; //uploadResult.SecureUrl.ToString();

        }
    }
}
