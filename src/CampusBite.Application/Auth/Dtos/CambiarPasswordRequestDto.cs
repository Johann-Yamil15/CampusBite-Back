using System.ComponentModel.DataAnnotations;

namespace CampusBite.Application.Auth.Dtos;

// JJ-Sprint5 13/11/2026: cambiar la contraseña con la sesión iniciada (mismas reglas que el registro)
public class CambiarPasswordRequestDto
{
    [Required(ErrorMessage = "La contraseña actual es obligatoria.")]
    public string PasswordActual { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña nueva es obligatoria.")]
    [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    [MaxLength(72, ErrorMessage = "La contraseña puede tener máximo 72 caracteres.")]
    public string PasswordNueva { get; set; } = string.Empty;
}
