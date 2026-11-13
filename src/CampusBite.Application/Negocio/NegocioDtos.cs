using System.ComponentModel.DataAnnotations;
using CampusBite.Domain.Promociones;

namespace CampusBite.Application.Negocio;

// JJ-Sprint5 13/11/2026: horario, promociones y estadísticas de la cafetería (forma de los sp_* 037/038/039)
public class HorarioDto
{
    public IReadOnlyList<DiaHorarioDto> Dias { get; init; } = [];
    public bool CerradoHoy { get; init; }
    public bool AbiertaAhora { get; init; }
}

public class DiaHorarioDto
{
    public bool Abre { get; set; }

    [Required, RegularExpression("^([01][0-9]|2[0-3]):[0-5][0-9]$", ErrorMessage = "La hora debe tener el formato HH:mm.")]
    public string Desde { get; set; } = "09:00";

    [Required, RegularExpression("^([01][0-9]|2[0-3]):[0-5][0-9]$", ErrorMessage = "La hora debe tener el formato HH:mm.")]
    public string Hasta { get; set; } = "17:00";
}

public class GuardarHorarioDto
{
    [Required(ErrorMessage = "El horario es obligatorio.")]
    [MinLength(7, ErrorMessage = "El horario debe tener exactamente 7 días.")]
    [MaxLength(7, ErrorMessage = "El horario debe tener exactamente 7 días.")]
    public List<DiaHorarioDto> Dias { get; set; } = [];
}

public class PromocionDto
{
    public Guid Id { get; init; }
    public required string Nombre { get; init; }
    public string Descripcion { get; init; } = string.Empty;
    public TipoPromocion Tipo { get; init; }
    public decimal Valor { get; init; }
    public DateOnly Desde { get; init; }
    public DateOnly Hasta { get; init; }
    public bool Activa { get; init; }
    public int Usos { get; init; }
}

// Porcentaje entero 1..90; PrecioFijo y Combo > 0 y ≤ 2000 (lo valida el procedimiento con su mensaje)
public class GuardarPromocionDto
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MinLength(3, ErrorMessage = "El nombre debe tener al menos 3 caracteres.")]
    [MaxLength(60, ErrorMessage = "El nombre puede tener máximo 60 caracteres.")]
    public string Nombre { get; set; } = string.Empty;

    [MaxLength(140, ErrorMessage = "La descripción puede tener máximo 140 caracteres.")]
    public string? Descripcion { get; set; }

    [Required(ErrorMessage = "El tipo es obligatorio.")]
    public TipoPromocion? Tipo { get; set; }

    [Range(0.01, 2000, ErrorMessage = "El valor debe ser mayor a 0 y máximo 2000.")]
    public decimal Valor { get; set; }

    [Required(ErrorMessage = "La fecha inicial es obligatoria.")]
    public DateOnly? Desde { get; set; }

    [Required(ErrorMessage = "La fecha final es obligatoria.")]
    public DateOnly? Hasta { get; set; }

    public bool Activa { get; set; } = true;
}

public class PromocionActivaDto
{
    public Guid IdPromocion { get; init; }
    public required string Etiqueta { get; init; }
    public required string Titulo { get; init; }
    public string Subtitulo { get; init; } = string.Empty;
    public string? ImagenUrl { get; init; }
    public Guid? IdCafeteria { get; init; }
    public string? Categoria { get; init; }
}

public class EstadisticasDto
{
    public decimal Ventas { get; init; }
    public int Pedidos { get; init; }
    public decimal TicketPromedio { get; init; }
    public int Cancelados { get; init; }
    public required PorEstadoDto PorEstado { get; init; }
    public IReadOnlyList<PuntoDto> VentasPorDia { get; init; } = [];
    public IReadOnlyList<MasVendidoDto> MasVendidos { get; init; } = [];
    public IReadOnlyList<PuntoDto> HorasPico { get; init; } = [];
}

public class PorEstadoDto
{
    public int Nuevos { get; init; }
    public int Preparacion { get; init; }
    public int Listos { get; init; }
    public int Entregados { get; init; }
    public int Cancelados { get; init; }
}

public record PuntoDto(string Etiqueta, decimal Valor);

public record MasVendidoDto(string Producto, int Piezas, decimal Monto);
