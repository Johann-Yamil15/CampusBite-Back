using System.ComponentModel.DataAnnotations;

namespace CampusBite.Application.Auth.Dtos;

// JY-Sprint4 31/10/2026: el token que llegó en el enlace del correo
public class VerificarCorreoRequestDto
{
    [Required(ErrorMessage = "Falta el token de verificación.")]
    [MaxLength(100, ErrorMessage = "El token de verificación no es válido.")]
    public string Token { get; set; } = string.Empty;
}
