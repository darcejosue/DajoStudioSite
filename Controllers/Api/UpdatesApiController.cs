using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DajoStudio.UpdateServer.Data;
using DajoStudio.UpdateServer.Models;
using DajoStudio.UpdateServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DajoStudio.UpdateServer.Controllers.Api
{
    [ApiController]
    [Route("api/updates")]
    public class UpdatesApiController : ControllerBase
    {
        private readonly UpdateDbContext _dbContext;
        private readonly IUpdateStorageService _storageService;

        public UpdatesApiController(UpdateDbContext dbContext, IUpdateStorageService storageService)
        {
            _dbContext = dbContext;
            _storageService = storageService;
        }

        /// <summary>
        /// Obtiene los detalles de la versión activa más reciente.
        /// </summary>
        [HttpGet("latest")]
        public async Task<IActionResult> GetLatest()
        {
            var latestRelease = await _dbContext.Releases
                .Where(r => r.IsActive)
                .OrderByDescending(r => r.CreatedUtc)
                .FirstOrDefaultAsync();

            if (latestRelease == null)
            {
                return NotFound(new { message = "No hay actualizaciones publicadas disponibles." });
            }

            string downloadUrl = Url.Action(nameof(DownloadFile), "UpdatesApi", new { fileName = latestRelease.FileName }, Request.Scheme) 
                                 ?? $"/api/updates/download/{latestRelease.FileName}";

            return Ok(new
            {
                version = latestRelease.Version,
                title = latestRelease.Title,
                releaseNotes = latestRelease.ReleaseNotes,
                fileName = latestRelease.FileName,
                fileSizeBytes = latestRelease.FileSizeBytes,
                sha256Hash = latestRelease.Sha256Hash,
                isMandatory = latestRelease.IsMandatory,
                createdUtc = latestRelease.CreatedUtc,
                downloadUrl = downloadUrl
            });
        }

        /// <summary>
        /// Comprueba si hay una versión superior a la versión enviada por el cliente.
        /// </summary>
        [HttpGet("check")]
        public async Task<IActionResult> CheckForUpdate([FromQuery] string currentVersion)
        {
            if (string.IsNullOrWhiteSpace(currentVersion))
            {
                return BadRequest(new { message = "El parámetro 'currentVersion' es obligatorio." });
            }

            var latestRelease = await _dbContext.Releases
                .Where(r => r.IsActive)
                .OrderByDescending(r => r.CreatedUtc)
                .FirstOrDefaultAsync();

            if (latestRelease == null)
            {
                return NoContent(); // 204 No update available
            }

            if (IsVersionNewer(latestRelease.Version, currentVersion))
            {
                string downloadUrl = Url.Action(nameof(DownloadFile), "UpdatesApi", new { fileName = latestRelease.FileName }, Request.Scheme) 
                                     ?? $"/api/updates/download/{latestRelease.FileName}";

                return Ok(new
                {
                    updateAvailable = true,
                    version = latestRelease.Version,
                    title = latestRelease.Title,
                    releaseNotes = latestRelease.ReleaseNotes,
                    fileName = latestRelease.FileName,
                    fileSizeBytes = latestRelease.FileSizeBytes,
                    sha256Hash = latestRelease.Sha256Hash,
                    isMandatory = latestRelease.IsMandatory,
                    downloadUrl = downloadUrl
                });
            }

            return NoContent();
        }

        /// <summary>
        /// Descarga el archivo de instalación por su nombre o versión. Soporta HTTP Range headers para descargas reanudables.
        /// El nombre de archivo entregado al usuario sigue el Título y Versión de la publicación.
        /// </summary>
        [HttpGet("download/{fileName}")]
        public async Task<IActionResult> DownloadFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return BadRequest();
            }

            var release = await _dbContext.Releases.FirstOrDefaultAsync(r => r.FileName == fileName || r.Version == fileName);
            string filePath = release != null ? _storageService.GetFilePath(release.FileName) : _storageService.GetFilePath(fileName);

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new { message = "El archivo solicitado no existe en el servidor." });
            }

            string extension = Path.GetExtension(filePath);
            string userFriendlyFileName = Path.GetFileName(filePath);

            if (release != null)
            {
                release.DownloadCount++;
                await _dbContext.SaveChangesAsync();

                if (!string.IsNullOrWhiteSpace(release.Title))
                {
                    string safeTitle = string.Join("_", release.Title.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Replace(" ", "_");
                    userFriendlyFileName = $"{safeTitle}_v{release.Version}{extension}";
                }
            }

            string contentType = "application/octet-stream";
            if (extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                contentType = "application/x-msdownload";
            else if (extension.Equals(".msi", StringComparison.OrdinalIgnoreCase))
                contentType = "application/x-ole-storage";
            else if (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                contentType = "application/zip";

            return PhysicalFile(filePath, contentType, userFriendlyFileName, enableRangeProcessing: true);
        }


        private static bool IsVersionNewer(string latestVersionStr, string currentVersionStr)
        {
            if (Version.TryParse(latestVersionStr, out Version? latest) &&
                Version.TryParse(currentVersionStr, out Version? current))
            {
                return latest > current;
            }

            return string.Compare(latestVersionStr, currentVersionStr, StringComparison.OrdinalIgnoreCase) > 0;
        }
    }
}
