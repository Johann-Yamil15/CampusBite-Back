using System.ComponentModel.DataAnnotations;

namespace CampusBite.Application.Productos.Dtos;

// JJ-Sprint4 27/10/2026: TEC-09. Datos para crear una categoría. La cafetería viene de la ruta, no del cuerpo.
public class CrearCategoriaDto : IValidatableObject
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MinLength(2, ErrorMessage = "El nombre debe tener al menos 2 caracteres.")]
    [MaxLength(60, ErrorMessage = "El nombre puede tener máximo 60 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Nombre) || Nombre.Trim().Length < 2)
            yield return new ValidationResult("El nombre debe tener al menos 2 caracteres.", [nameof(Nombre)]);
    }
}
