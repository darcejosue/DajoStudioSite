using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DajoStudio.UpdateServer.Services
{
    public class UpdateStorageService : IUpdateStorageService
    {
        private readonly string _storageDirectory;
        private readonly ILogger<UpdateStorageService> _logger;

        public UpdateStorageService(IWebHostEnvironment env, ILogger<UpdateStorageService> logger)
        {
            _logger = logger;
            _storageDirectory = Path.Combine(env.WebRootPath, "updates");

            if (!Directory.Exists(_storageDirectory))
            {
                Directory.CreateDirectory(_storageDirectory);
            }
        }

        public async Task<(string fileName, long sizeBytes, string sha256Hash)> SaveFileAsync(IFormFile file, string version)
        {
            if (file == null || file.Length == 0)
            {
                throw new ArgumentException("El archivo proporcionado está vacío.", nameof(file));
            }

            string extension = Path.GetExtension(file.FileName);
            string safeVersionStr = version.Replace('.', '_');
            string uniqueFileName = $"update_v{safeVersionStr}_{DateTime.UtcNow:yyyyMMddHHmmss}{extension}";
            string destinationPath = Path.Combine(_storageDirectory, uniqueFileName);

            _logger.LogInformation("Iniciando guardado de archivo {OriginalName} ({Size} bytes) como {UniqueName}", file.FileName, file.Length, uniqueFileName);

            long totalBytesRead = 0;
            string sha256HashStr;

            using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true))
            using (var sha256 = SHA256.Create())
            using (var inputStream = file.OpenReadStream())
            {
                byte[] buffer = new byte[81920]; // 80 KB chunk buffer
                int bytesRead;

                while ((bytesRead = await inputStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
                    totalBytesRead += bytesRead;
                }

                sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                byte[] hashBytes = sha256.Hash ?? Array.Empty<byte>();
                sha256HashStr = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }

            _logger.LogInformation("Archivo guardado exitosamente. Hash SHA256: {Hash}", sha256HashStr);

            return (uniqueFileName, totalBytesRead, sha256HashStr);
        }

        public Task<bool> DeleteFileAsync(string fileName)
        {
            try
            {
                string filePath = GetFilePath(fileName);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation("Archivo eliminado: {FilePath}", filePath);
                    return Task.FromResult(true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar archivo {FileName}", fileName);
            }

            return Task.FromResult(false);
        }

        public string GetFilePath(string fileName)
        {
            return Path.Combine(_storageDirectory, Path.GetFileName(fileName));
        }

        public bool FileExists(string fileName)
        {
            return File.Exists(GetFilePath(fileName));
        }
    }
}
