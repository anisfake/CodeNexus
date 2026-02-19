using CodeNexus.Application.Common.Interfaces;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace CodeNexus.Infrastructure.Services
{
    public class EncryptionService : IEncryptionService
    {
        private readonly IConfiguration _configuration;

        public EncryptionService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string Encrypt(string plainText)
        {
            var key = Encoding.UTF8.GetBytes(_configuration["Encryption:Key"] ?? throw new InvalidOperationException("Encryption key not configured"));
            
            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
                {
                    using (var ms = new MemoryStream())
                    {
                        ms.Write(aes.IV, 0, aes.IV.Length);
                        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                        {
                            using (var sw = new StreamWriter(cs))
                            {
                                sw.Write(plainText);
                            }
                        }
                        return Convert.ToBase64String(ms.ToArray());
                    }
                }
            }
        }

        public string Decrypt(string cipherText)
        {
            var key = Encoding.UTF8.GetBytes(_configuration["Encryption:Key"] ?? throw new InvalidOperationException("Encryption key not configured"));
            var buffer = Convert.FromBase64String(cipherText);

            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                var iv = new byte[aes.IV.Length];
                Array.Copy(buffer, 0, iv, 0, iv.Length);
                aes.IV = iv;

                using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                {
                    using (var ms = new MemoryStream(buffer, iv.Length, buffer.Length - iv.Length))
                    {
                        using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                        {
                            using (var sr = new StreamReader(cs))
                            {
                                return sr.ReadToEnd();
                            }
                        }
                    }
                }
            }
        }
    }
}
