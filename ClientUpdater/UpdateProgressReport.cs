using System;

namespace DajoStudio.ClientUpdater
{
    public class UpdateProgressReport
    {
        public double Percentage { get; set; }
        public long BytesDownloaded { get; set; }
        public long TotalBytes { get; set; }
        public double SpeedMBps { get; set; }
        public TimeSpan EstimatedTimeRemaining { get; set; }
        public string StatusMessage { get; set; } = string.Empty;

        public string FormattedDownloaded => $"{BytesDownloaded / (1024.0 * 1024.0):F1} MB";
        public string FormattedTotal => $"{TotalBytes / (1024.0 * 1024.0):F1} MB";
    }
}
