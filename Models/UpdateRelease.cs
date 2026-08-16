using System;
using System.ComponentModel.DataAnnotations;

namespace DajoStudio.UpdateServer.Models
{
    public class UpdateRelease
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Version { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000)]
        public string ReleaseNotes { get; set; } = string.Empty;

        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;

        public long FileSizeBytes { get; set; }

        [Required]
        [StringLength(64)]
        public string Sha256Hash { get; set; } = string.Empty;

        public bool IsMandatory { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        public int DownloadCount { get; set; }

        public string FormattedSize
        {
            get
            {
                double mb = FileSizeBytes / (1024.0 * 1024.0);
                if (mb >= 1024.0)
                {
                    return $"{mb / 1024.0:F2} GB";
                }
                return $"{mb:F1} MB";
            }
        }
    }
}
