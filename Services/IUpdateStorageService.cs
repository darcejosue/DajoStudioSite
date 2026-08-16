using System.IO;
using System.Threading.Tasks;
using DajoStudio.UpdateServer.Models;
using Microsoft.AspNetCore.Http;

namespace DajoStudio.UpdateServer.Services
{
    public interface IUpdateStorageService
    {
        Task<(string fileName, long sizeBytes, string sha256Hash)> SaveFileAsync(IFormFile file, string version);
        Task<bool> DeleteFileAsync(string fileName);
        string GetFilePath(string fileName);
        bool FileExists(string fileName);
    }
}
