using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace DajoStudio.UpdateServer.Models
{
    public class UploadUpdateViewModel
    {
        [Required(ErrorMessage = "La versión es requerida (ej. 1.0.5)")]
        [RegularExpression(@"^\d+\.\d+\.\d+(\.\d+)?$", ErrorMessage = "Formato de versión inválido. Usa formato SemVer (ej. 1.0.0 o 1.2.3.4)")]
        public string Version { get; set; } = string.Empty;

        [Required(ErrorMessage = "El título de la actualización es requerido")]
        [StringLength(100, ErrorMessage = "El título no puede exceder 100 caracteres")]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000)]
        public string ReleaseNotes { get; set; } = string.Empty;

        [Required(ErrorMessage = "Debes seleccionar un archivo de actualización (.exe, .zip, .msi, etc.)")]
        public IFormFile? File { get; set; }

        public bool IsMandatory { get; set; }
    }
}
