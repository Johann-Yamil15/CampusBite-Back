using System.ComponentModel.DataAnnotations;

namespace CampusBite.Application.Auth.Dtos;

// JY-Sprint4 31/10/2026: pedir otro correo de verificación. La respuesta es igual exista o no la cuenta.
public class ReenviarVerificacionRequestDto
{
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    [MaxLength(120, ErrorMessage = "El correo puede tener máximo 120 caracteres.")]
    public string Correo { get; set; } = string.Empty;
}
