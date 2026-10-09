using System.ComponentModel.DataAnnotations;

namespace CampusBite_Back.Models.Dtos.Auth;

// JJ-Sprint2 09/10/2026: datos de entrada para iniciar sesión, con mensajes de validación en español
public class LoginRequestDto
{
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    public string Correo { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    public string Password { get; set; } = string.Empty;
}
