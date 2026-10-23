using System.ComponentModel.DataAnnotations;
using CampusBite.Domain.Cafeterias;

namespace CampusBite.Application.Auth.Dtos;

// JJ-Sprint3 22/10/2026: TEC-11. Autorregistro público de un negocio: el encargado y su cafetería en un solo paso.
// Reusa las reglas y mensajes de RegistroRequestDto (encargado) y GuardarCafeteriaDto (cafetería); el contrato
// lo fija el front en auth.dto.ts (RegistroCafeteriaRequestDto).
public class RegistroCafeteriaRequestDto : IValidatableObject
{
    // --- Encargado (mismas reglas que RegistroRequestDto) ---
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MinLength(2, ErrorMessage = "El nombre debe tener al menos 2 caracteres.")]
    [MaxLength(80, ErrorMessage = "El nombre puede tener máximo 80 caracteres.")]
    public string NombreEncargado { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    [MaxLength(120, ErrorMessage = "El correo puede tener máximo 120 caracteres.")]
    public string Correo { get; set; } = string.Empty;

    // BCrypt solo toma en cuenta los primeros 72 bytes; más largo daría una falsa sensación de seguridad
    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
    [MaxLength(72, ErrorMessage = "La contraseña puede tener máximo 72 caracteres.")]
    public string Password { get; set; } = string.Empty;

    [RegularExpression(@"^[0-9+]{10,15}$", ErrorMessage = "El teléfono debe tener de 10 a 15 dígitos.")]
    public string? Telefono { get; set; }

    // --- Cafetería (mismas reglas que GuardarCafeteriaDto) ---
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MinLength(2, ErrorMessage = "El nombre debe tener al menos 2 caracteres.")]
    [MaxLength(100, ErrorMessage = "El nombre puede tener máximo 100 caracteres.")]
    public string NombreCafeteria { get; set; } = string.Empty;

    [Required(ErrorMessage = "La ubicación es obligatoria.")]
    [MaxLength(150, ErrorMessage = "La ubicación puede tener máximo 150 caracteres.")]
    public string Ubicacion { get; set; } = string.Empty;

    // bool? para que "no lo mandó" sea un error y no un false silencioso
    [Required(ErrorMessage = "Indica si la cafetería es del campus.")]
    public bool? EsDelCampus { get; set; }

    [Required(ErrorMessage = "El punto de entrega es obligatorio.")]
    [MaxLength(100, ErrorMessage = "El punto de entrega puede tener máximo 100 caracteres.")]
    public string PuntoEntrega { get; set; } = string.Empty;

    [Required(ErrorMessage = "Indica los métodos de pago.")]
    public List<MetodoPago> MetodosPago { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Solo espacios pasan [MinLength]; se revisa el texto recortado, como en la BD (length(btrim(...)))
        if (string.IsNullOrWhiteSpace(NombreEncargado) || NombreEncargado.Trim().Length < 2)
            yield return new ValidationResult("El nombre debe tener al menos 2 caracteres.", [nameof(NombreEncargado)]);
        if (string.IsNullOrWhiteSpace(NombreCafeteria) || NombreCafeteria.Trim().Length < 2)
            yield return new ValidationResult("El nombre debe tener al menos 2 caracteres.", [nameof(NombreCafeteria)]);
        if (string.IsNullOrWhiteSpace(Ubicacion))
            yield return new ValidationResult("La ubicación es obligatoria.", [nameof(Ubicacion)]);
        if (string.IsNullOrWhiteSpace(PuntoEntrega))
            yield return new ValidationResult("El punto de entrega es obligatorio.", [nameof(PuntoEntrega)]);

        if (MetodosPago is null || MetodosPago.Count == 0)
            yield return new ValidationResult("La cafetería debe aceptar al menos un método de pago.", [nameof(MetodosPago)]);
        // JSON acepta números en un enum ("metodosPago": [7]): se rechaza cualquier valor que no exista
        else if (MetodosPago.Any(m => !Enum.IsDefined(m)))
            yield return new ValidationResult("Método de pago no válido. Usa: Tarjeta, Transferencia o Efectivo.", [nameof(MetodosPago)]);
    }
}
