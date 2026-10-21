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

    // Solo corre si los atributos pasaron: [Required] ya rechaza nulos y textos de puros espacios.
    // Aquí va lo que los atributos no pueden expresar.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // MinLength cuenta los espacios: " A " mide 3 pero solo tiene 1 carácter real
        if (Nombre.Trim().Length < 2)
            yield return new ValidationResult("El nombre debe tener al menos 2 caracteres.", [nameof(Nombre)]);

        if (MetodosPago.Count == 0)
            yield return new ValidationResult("La cafetería debe aceptar al menos un método de pago.", [nameof(MetodosPago)]);
        // La API ya rechaza números en el JSON; esto cubre un valor fuera del enum que llegue por código
        else if (MetodosPago.Any(m => !Enum.IsDefined(m)))
            yield return new ValidationResult("Método de pago no válido. Usa: Tarjeta, Transferencia o Efectivo.", [nameof(MetodosPago)]);
    }
}
