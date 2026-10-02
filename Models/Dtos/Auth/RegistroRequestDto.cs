using System.ComponentModel.DataAnnotations;

namespace CampusBite_Back.Models.Dtos.Auth;

// JJ-Sprint1 02/10/2026: datos de entrada para registrar un usuario
public class RegistroRequestDto
{
    [Required, MaxLength(100)]
    public string Nombre { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(150)]
    public string Correo { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;
}
