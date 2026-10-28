using System.ComponentModel.DataAnnotations;

namespace CampusBite.Application.Productos.Dtos;

// JY-Sprint4 27/10/2026: TEC-09. Datos para crear un producto (y editarlo, cuando exista el PUT).
// Reglas iguales a las de la BD (tabla producto y sp_validar_datos_producto), con mensajes en español.
// La imagen no va aquí: es un archivo y lo agrega el formulario de la Api (CrearProductoFormDto).
public class GuardarProductoDto : IValidatableObject
{
    public const decimal PrecioMaximo = 999_999.99m;   // NUMERIC(8,2)

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MaxLength(100, ErrorMessage = "El nombre puede tener máximo 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "La descripción puede tener máximo 500 caracteres.")]
    public string? Descripcion { get; set; }

    // decimal? para que "no lo mandó" sea un error y no un 0 silencioso
    [Required(ErrorMessage = "El precio es obligatorio.")]
    public decimal? Precio { get; set; }

    // Opcional; debe ser una categoría de la misma cafetería (lo revisa la BD)
    public Guid? IdCategoria { get; set; }

    public bool Disponible { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Nombre) || Nombre.Trim().Length < 2)
            yield return new ValidationResult("El nombre debe tener al menos 2 caracteres.", [nameof(Nombre)]);

        if (Precio is { } precio && (precio <= 0 || precio > PrecioMaximo))
            yield return new ValidationResult("El precio debe ser mayor a 0 y menor a 1,000,000.", [nameof(Precio)]);
        else if (Precio is { } conDecimales && conDecimales != decimal.Round(conDecimales, 2))
            yield return new ValidationResult("El precio puede tener máximo 2 decimales.", [nameof(Precio)]);

        if (IdCategoria == Guid.Empty)
            yield return new ValidationResult("La categoría no es válida.", [nameof(IdCategoria)]);
    }
}
