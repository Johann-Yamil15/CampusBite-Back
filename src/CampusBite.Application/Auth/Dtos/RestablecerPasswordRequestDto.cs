using System.ComponentModel.DataAnnotations;

namespace CampusBite.Application.Auth.Dtos;

// JJ-Sprint3 21/10/2026: TEC-12. Recuperación simple: correo + nueva contraseña, sin código de verificación
// (se agrega en una historia futura). El endpoint responde igual exista o no el correo, para no revelarlo.
public class RestablecerPasswordRequestDto
{
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    public string Correo { get; set; } = string.Empty;

    [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
    [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    [MaxLength(72, ErrorMessage = "La contraseña puede tener máximo 72 caracteres.")]
    public string NuevaPassword { get; set; } = string.Empty;
}
