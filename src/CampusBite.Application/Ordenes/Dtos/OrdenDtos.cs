using System.ComponentModel.DataAnnotations;
using CampusBite.Domain.Cafeterias;
using CampusBite.Domain.Ordenes;

namespace CampusBite.Application.Ordenes.Dtos;

// JJ-Sprint5 13/11/2026: contratos de órdenes (alumno y tablero), forma de fn_orden_json en la BD.
// CodigoRecogida solo llega al alumno dueño (null para la cafetería); Alumno solo a la cafetería.
public class OrdenDto
{
    public Guid IdOrden { get; init; }
    public Guid IdCafeteria { get; init; }
    public required string Cafeteria { get; init; }
    public string? LogoCafeteria { get; init; }
    public string PuntoEntrega { get; init; } = string.Empty;
    public AlumnoOrdenDto? Alumno { get; init; }
    public EstadoOrden Estado { get; init; }
    public DateTimeOffset RecogidaEn { get; init; }
    public DateTimeOffset CreadoEn { get; init; }
    public string? CodigoRecogida { get; init; }
    public decimal Total { get; init; }
    public MetodoPago Metodo { get; init; }
    public EstadoPago EstadoPago { get; init; }
    public string? Notas { get; init; }
    public string? MotivoCancelacion { get; init; }
    public IReadOnlyList<LineaOrdenDto> Lineas { get; init; } = [];
}

public class AlumnoOrdenDto
{
    public required string Nombre { get; init; }
    public string? Matricula { get; init; }
}

public class LineaOrdenDto
{
    public Guid IdProducto { get; init; }
    public required string Producto { get; init; }
    public int Cantidad { get; init; }
    public decimal PrecioUnitario { get; init; }
    public string? ImagenUrl { get; init; }
}

public class CrearOrdenDto
{
    [Required(ErrorMessage = "La cafetería es obligatoria.")]
    public Guid? IdCafeteria { get; set; }

    [Required(ErrorMessage = "La hora de recogida es obligatoria.")]
    public DateTimeOffset? RecogidaEn { get; set; }

    [Required(ErrorMessage = "El método de pago es obligatorio.")]
    public MetodoPago? Metodo { get; set; }

    [Required(ErrorMessage = "La orden debe incluir al menos un producto.")]
    [MinLength(1, ErrorMessage = "La orden debe incluir al menos un producto.")]
    [MaxLength(50, ErrorMessage = "La orden puede tener máximo 50 productos.")]
    public List<ItemOrdenDto> Items { get; set; } = [];

    [MaxLength(200, ErrorMessage = "Las notas pueden tener máximo 200 caracteres.")]
    public string? Notas { get; set; }
}

public class ItemOrdenDto
{
    [Required(ErrorMessage = "El producto es obligatorio.")]
    public Guid? IdProducto { get; set; }

    [Range(1, 20, ErrorMessage = "La cantidad por producto debe estar entre 1 y 20.")]
    public int Cantidad { get; set; }
}

public class CambiarEstadoOrdenDto
{
    [Required(ErrorMessage = "El estado es obligatorio.")]
    public EstadoOrden? Estado { get; set; }
}

public class EntregarOrdenDto
{
    [Required(ErrorMessage = "El código de recogida es obligatorio.")]
    [RegularExpression("^[0-9]{6}$", ErrorMessage = "El código de recogida son 6 dígitos.")]
    public string CodigoRecogida { get; set; } = string.Empty;
}

public class RechazarOrdenDto
{
    [Required(ErrorMessage = "El motivo es obligatorio.")]
    [MinLength(5, ErrorMessage = "El motivo debe tener al menos 5 caracteres.")]
    [MaxLength(200, ErrorMessage = "El motivo puede tener máximo 200 caracteres.")]
    public string Motivo { get; set; } = string.Empty;
}

public class ConfirmarPagoDto
{
    [MaxLength(100, ErrorMessage = "La referencia puede tener máximo 100 caracteres.")]
    public string? Referencia { get; set; }
}

public class CrearReporteDto
{
    [Required(ErrorMessage = "El motivo es obligatorio.")]
    public MotivoReporte? Motivo { get; set; }

    [MaxLength(300, ErrorMessage = "El detalle puede tener máximo 300 caracteres.")]
    public string? Detalle { get; set; }
}

public record ReporteCreadoDto(string Folio);
