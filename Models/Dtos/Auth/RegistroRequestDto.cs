using System.ComponentModel.DataAnnotations;

namespace CampusBite_Back.Models.Dtos.Auth;

// JJ-Sprint1 04/10/2026: datos para registrar un alumno; las reglas siguen los CHECK de la tabla usuario
public class RegistroRequestDto
{
    [Required, MinLength(2), MaxLength(80)]
    public string Nombre { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(120)]
    public string Correo { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Matricula { get; set; }

    [RegularExpression(@"^[0-9+]{10,15}$", ErrorMessage = "El teléfono debe tener de 10 a 15 dígitos.")]
    public string? Telefono { get; set; }
}
