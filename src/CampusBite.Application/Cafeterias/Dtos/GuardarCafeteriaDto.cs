using System.ComponentModel.DataAnnotations;
using CampusBite.Domain.Cafeterias;

namespace CampusBite.Application.Cafeterias.Dtos;

// JY-Sprint3 20/10/2026: TEC-08. Datos para crear o editar una cafetería (el mismo cuerpo en POST y PUT).
// Reglas iguales a las de la BD (tabla cafeteria y sp_validar_datos_cafeteria), con mensajes en español.
// "activa" no va aquí: la baja y la reactivación son operaciones aparte y solo las hace admin_sistema.
public class GuardarCafeteriaDto : IValidatableObject
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MinLength(2, ErrorMessage = "El nombre debe tener al menos 2 caracteres.")]
    [MaxLength(100, ErrorMessage = "El nombre puede tener máximo 100 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

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
        if (string.IsNullOrWhiteSpace(Nombre) || Nombre.Trim().Length < 2)
            yield return new ValidationResult("El nombre debe tener al menos 2 caracteres.", [nameof(Nombre)]);
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
