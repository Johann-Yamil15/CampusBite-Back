using CampusBite.Domain.Cafeterias;

namespace CampusBite.Application.Menu.Dtos;

// JY-Sprint4 05/11/2026: HU-07B. El menú de una cafetería activa con sus productos disponibles y los
// métodos de pago que acepta (el alumno los necesita antes de confirmar). Forma de sp_menu_cafeteria.
// JJ-Sprint5 13/11/2026: + imágenes, abierta/cierraA (horario y cierre de hoy) y tiempo estimado.
// Calificacion y Resenas van null mientras no haya reseñas.
public class MenuCafeteriaDto
{
    public Guid IdCafeteria { get; init; }
    public required string Cafeteria { get; init; }
    public bool EsDelCampus { get; init; }
    public string PuntoEntrega { get; init; } = string.Empty;
    public IReadOnlyList<MetodoPago> MetodosPago { get; init; } = [];
    public string? LogoUrl { get; init; }
    public string? PortadaUrl { get; init; }
    public decimal? Calificacion { get; init; }
    public int? Resenas { get; init; }
    public bool Abierta { get; init; }
    public string? CierraA { get; init; }
    public int? MinutosEstimados { get; init; }
    public IReadOnlyList<ProductoMenuDto> Productos { get; init; } = [];
}
