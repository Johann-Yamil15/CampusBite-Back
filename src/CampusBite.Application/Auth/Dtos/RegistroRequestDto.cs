using System.ComponentModel.DataAnnotations;

namespace CampusBite.Application.Auth.Dtos;

// JJ-Sprint2 09/10/2026: datos para registrar un alumno; reglas de los CHECK de la tabla usuario y mensajes en español
public class RegistroRequestDto
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MinLength(2, ErrorMessage = "El nombre debe tener al menos 2 caracteres.")]
    [MaxLength(80, ErrorMessage = "El nombre puede tener máximo 80 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    [MaxLength(120, ErrorMessage = "El correo puede tener máximo 120 caracteres.")]
    public string Correo { get; set; } = string.Empty;

    // BCrypt solo toma en cuenta los primeros 72 bytes; más largo daría una falsa sensación de seguridad
    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    [MaxLength(72, ErrorMessage = "La contraseña puede tener máximo 72 caracteres.")]
    public string Password { get; set; } = string.Empty;

    [MaxLength(20, ErrorMessage = "La matrícula puede tener máximo 20 caracteres.")]
    public string? Matricula { get; set; }

    [RegularExpression(@"^[0-9+]{10,15}$", ErrorMessage = "El teléfono debe tener de 10 a 15 dígitos.")]
    public string? Telefono { get; set; }
}
