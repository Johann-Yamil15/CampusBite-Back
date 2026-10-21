using CampusBite.Domain.Cafeterias;

namespace CampusBite.Application.Cafeterias.Dtos;

// JY-Sprint3 20/10/2026: TEC-08. Una cafetería como la devuelve la API (misma forma en lista y detalle)
public class CafeteriaDto
{
    public Guid IdCafeteria { get; init; }
    public string Nombre { get; init; } = string.Empty;
    public string Ubicacion { get; init; } = string.Empty;
    public bool EsDelCampus { get; init; }
    public string PuntoEntrega { get; init; } = string.Empty;
    public bool Activa { get; init; }
    public IReadOnlyList<MetodoPago> MetodosPago { get; init; } = [];
    public DateTimeOffset CreadoEn { get; init; }
    public DateTimeOffset ActualizadoEn { get; init; }
}
