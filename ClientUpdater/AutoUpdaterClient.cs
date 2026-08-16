using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DajoStudio.ClientUpdater
{
    public class UpdateCheckResult
    {
        public bool UpdateAvailable { get; set; }
        public string Version { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public string Sha256Hash { get; set; } = string.Empty;
        public bool IsMandatory { get; set; }
        public string DownloadUrl { get; set; } = string.Empty;
    }

    public class AutoUpdaterClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _serverBaseUrl;

        public AutoUpdaterClient(string serverBaseUrl, HttpClient? httpClient = null)
        {
            _serverBaseUrl = serverBaseUrl.TrimEnd('/');
            _httpClient = httpClient ?? new HttpClient();
            _httpClient.Timeout = TimeSpan.FromHours(2); // Timeout for 250MB+ downloads
        }

        public async Task<UpdateCheckResult?> CheckForUpdateAsync(string currentVersion, CancellationToken ct = default)
        {
            string requestUrl = $"{_serverBaseUrl}/api/updates/check?currentVersion={Uri.EscapeDataString(currentVersion)}";
            
            var response = await _httpClient.GetAsync(requestUrl, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                return new UpdateCheckResult { UpdateAvailable = false };
            }

            response.EnsureSuccessStatusCode();
            string json = await response.Content.ReadAsStringAsync(ct);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            return new UpdateCheckResult
            {
                UpdateAvailable = true,
                Version = root.GetProperty("version").GetString() ?? string.Empty,
                Title = root.GetProperty("title").GetString() ?? string.Empty,
                ReleaseNotes = root.GetProperty("releaseNotes").GetString() ?? string.Empty,
                FileName = root.GetProperty("fileName").GetString() ?? string.Empty,
                FileSizeBytes = root.GetProperty("fileSizeBytes").GetInt64(),
                Sha256Hash = root.GetProperty("sha256Hash").GetString() ?? string.Empty,
                IsMandatory = root.GetProperty("isMandatory").GetBoolean(),
                DownloadUrl = root.GetProperty("downloadUrl").GetString() ?? string.Empty
            };
        }

        public async Task<string> DownloadUpdateAsync(
            UpdateCheckResult updateInfo, 
            IProgress<UpdateProgressReport>? progress = null, 
            CancellationToken ct = default)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "DajoStudioUpdates");
            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }

            string destinationPath = Path.Combine(tempDir, updateInfo.FileName);

            long existingFileLength = File.Exists(destinationPath) ? new FileInfo(destinationPath).Length : 0;
            long totalBytes = updateInfo.FileSizeBytes;

            // If local file is complete and SHA256 matches, reuse it!
            if (existingFileLength == totalBytes && File.Exists(destinationPath))
            {
                string existingHash = VerifySha256(destinationPath);
                if (existingHash.Equals(updateInfo.Sha256Hash, StringComparison.OrdinalIgnoreCase))
                {
                    progress?.Report(new UpdateProgressReport
                    {
                        Percentage = 100.0,
                        BytesDownloaded = totalBytes,
                        TotalBytes = totalBytes,
                        StatusMessage = "Archivo existente verificado."
                    });
                    return destinationPath;
                }
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, updateInfo.DownloadUrl);
            
            if (existingFileLength > 0 && existingFileLength < totalBytes)
            {
                request.Headers.Range = new RangeHeaderValue(existingFileLength, null);
            }
            else
            {
                existingFileLength = 0; // Restart fresh if corrupted or invalid size
            }

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            using var networkStream = await response.Content.ReadAsStreamAsync(ct);
            using var fileStream = new FileStream(
                destinationPath, 
                existingFileLength > 0 ? FileMode.Append : FileMode.Create, 
                FileAccess.Write, 
                FileShare.None, 
                bufferSize: 81920, 
                useAsync: true);

            byte[] buffer = new byte[81920];
            long totalDownloaded = existingFileLength;
            int bytesRead;

            DateTime startTime = DateTime.UtcNow;
            long lastReadBytes = totalDownloaded;
            DateTime lastTime = startTime;

            while ((bytesRead = await networkStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, ct);
                totalDownloaded += bytesRead;

                DateTime now = DateTime.UtcNow;
                double timeDiffSec = (now - lastTime).TotalSeconds;

                if (timeDiffSec >= 0.3)
                {
                    double speedBytesPerSec = (totalDownloaded - lastReadBytes) / timeDiffSec;
                    double speedMBps = speedBytesPerSec / (1024.0 * 1024.0);
                    double percent = totalBytes > 0 ? ((double)totalDownloaded / totalBytes) * 100.0 : 0.0;

                    long remainingBytes = totalBytes - totalDownloaded;
                    TimeSpan eta = speedBytesPerSec > 0 ? TimeSpan.FromSeconds(remainingBytes / speedBytesPerSec) : TimeSpan.Zero;

                    progress?.Report(new UpdateProgressReport
                    {
                        Percentage = percent,
                        BytesDownloaded = totalDownloaded,
                        TotalBytes = totalBytes,
                        SpeedMBps = speedMBps,
                        EstimatedTimeRemaining = eta,
                        StatusMessage = $"Descargando instalador ({totalDownloaded / (1024.0 * 1024.0):F1} MB de {totalBytes / (1024.0 * 1024.0):F1} MB)"
                    });

                    lastReadBytes = totalDownloaded;
                    lastTime = now;
                }
            }

            // Verify integrity after download completes
            progress?.Report(new UpdateProgressReport
            {
                Percentage = 100.0,
                BytesDownloaded = totalBytes,
                TotalBytes = totalBytes,
                StatusMessage = "Verificando firma digital SHA256..."
            });

            string calculatedHash = VerifySha256(destinationPath);
            if (!calculatedHash.Equals(updateInfo.Sha256Hash, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(destinationPath);
                throw new InvalidDataException("La verificación de integridad SHA256 del instalador descargado ha fallado.");
            }

            return destinationPath;
        }

        public string VerifySha256(string filePath)
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(filePath);
            byte[] hashBytes = sha256.ComputeHash(stream);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }

        public void ExecuteInstallerAndExit(string installerPath, string silentArguments = "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART")
        {
            if (!File.Exists(installerPath))
            {
                throw new FileNotFoundException("El archivo instalador no fue encontrado.", installerPath);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = silentArguments,
                UseShellExecute = true
            };

            Process.Start(startInfo);
            Environment.Exit(0);
        }
    }
}
