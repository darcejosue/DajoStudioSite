using System;
using System.Linq;
using System.Threading.Tasks;
using DajoStudio.UpdateServer.Data;
using DajoStudio.UpdateServer.Models;
using DajoStudio.UpdateServer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DajoStudio.UpdateServer.Controllers
{
    public class HomeController : Controller
    {
        private readonly UpdateDbContext _dbContext;
        private readonly IUpdateStorageService _storageService;

        public HomeController(UpdateDbContext dbContext, IUpdateStorageService storageService)
        {
            _dbContext = dbContext;
            _storageService = storageService;
        }

        public async Task<IActionResult> Index()
        {
            var releases = await _dbContext.Releases
                .OrderByDescending(r => r.CreatedUtc)
                .ToListAsync();

            long totalSizeBytes = releases.Sum(r => r.FileSizeBytes);
            int totalDownloads = releases.Sum(r => r.DownloadCount);
            var latestRelease = releases.FirstOrDefault(r => r.IsActive);

            ViewBag.TotalSizeBytes = totalSizeBytes;
            ViewBag.TotalDownloads = totalDownloads;
            ViewBag.LatestRelease = latestRelease;

            return View(releases);
        }

        [HttpGet]
        public IActionResult Upload()
        {
            return View(new UploadUpdateViewModel());
        }

        [HttpPost]
        [RequestSizeLimit(1_073_741_824)] // 1 GB upload limit
        [RequestFormLimits(MultipartBodyLengthLimit = 1_073_741_824, ValueLengthLimit = 1_073_741_824)]
        public async Task<IActionResult> Upload(UploadUpdateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    return BadRequest(new { success = false, message = string.Join(" ", errors) });
                }
                return View(model);
            }

            // Check if version already exists
            bool versionExists = await _dbContext.Releases.AnyAsync(r => r.Version == model.Version.Trim());
            if (versionExists)
            {
                string msg = $"La versión {model.Version} ya existe en el servidor.";
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return BadRequest(new { success = false, message = msg });
                }
                ModelState.AddModelError("Version", msg);
                return View(model);
            }

            try
            {
                if (model.File == null || model.File.Length == 0)
                {
                    throw new Exception("No se ha seleccionado ningún archivo.");
                }

                var (fileName, sizeBytes, sha256Hash) = await _storageService.SaveFileAsync(model.File, model.Version);

                var release = new UpdateRelease
                {
                    Version = model.Version.Trim(),
                    Title = model.Title.Trim(),
                    ReleaseNotes = model.ReleaseNotes?.Trim() ?? string.Empty,
                    FileName = fileName,
                    FileSizeBytes = sizeBytes,
                    Sha256Hash = sha256Hash,
                    IsMandatory = model.IsMandatory,
                    IsActive = true,
                    CreatedUtc = DateTime.UtcNow,
                    DownloadCount = 0
                };

                _dbContext.Releases.Add(release);
                await _dbContext.SaveChangesAsync();

                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return Ok(new { 
                        success = true, 
                        message = "Actualización subida exitosamente.", 
                        version = release.Version,
                        size = release.FormattedSize,
                        hash = release.Sha256Hash
                    });
                }

                TempData["SuccessMessage"] = $"La actualización v{release.Version} ({release.FormattedSize}) ha sido publicada exitosamente.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                string errorMsg = $"Error al procesar la subida: {ex.Message}";
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return StatusCode(500, new { success = false, message = errorMsg });
                }
                ModelState.AddModelError(string.Empty, errorMsg);
                return View(model);
            }
        }

        [HttpPost]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var release = await _dbContext.Releases.FindAsync(id);
            if (release != null)
            {
                release.IsActive = !release.IsActive;
                await _dbContext.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Estado de la versión v{release.Version} actualizado a {(release.IsActive ? "Activo" : "Inactivo")}.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var release = await _dbContext.Releases.FindAsync(id);
            if (release != null)
            {
                await _storageService.DeleteFileAsync(release.FileName);
                _dbContext.Releases.Remove(release);
                await _dbContext.SaveChangesAsync();
                TempData["SuccessMessage"] = $"La actualización v{release.Version} fue eliminada del servidor.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet("Download/{version?}")]
        [HttpGet("Updates/Release/{version?}")]
        public async Task<IActionResult> DownloadPage(string? version)
        {
            UpdateRelease? release;
            if (string.IsNullOrWhiteSpace(version) || version.Equals("latest", StringComparison.OrdinalIgnoreCase))
            {
                release = await _dbContext.Releases
                    .Where(r => r.IsActive)
                    .OrderByDescending(r => r.CreatedUtc)
                    .FirstOrDefaultAsync();
            }
            else
            {
                release = await _dbContext.Releases.FirstOrDefaultAsync(r => r.Version == version || r.FileName == version);
            }

            if (release == null)
            {
                return NotFound("La actualización solicitada no fue encontrada o ha sido retirada del servidor.");
            }

            return View("DownloadPage", release);
        }

        public IActionResult IntegrationGuide()
        {
            return View();
        }
    }
}

