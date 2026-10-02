using System.ComponentModel.DataAnnotations;

namespace CampusBite_Back.Models.Dtos.Auth;

// JJ-Sprint1 02/10/2026: datos de entrada para iniciar sesión
public class LoginRequestDto
{
    [Required, EmailAddress]
    public string Correo { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}
