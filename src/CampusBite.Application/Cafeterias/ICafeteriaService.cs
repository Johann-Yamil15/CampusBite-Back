using CampusBite.Application.Cafeterias.Dtos;

namespace CampusBite.Application.Cafeterias;

// JY-Sprint3 20/10/2026: TEC-08. Casos de uso de cafeterías. Quién actúa sale de IUsuarioActual, no de los parámetros.
public interface ICafeteriaService
{
    Task<IReadOnlyList<CafeteriaDto>> ListarAsync(bool incluirInactivas, CancellationToken cancellationToken = default);
    Task<CafeteriaDto> ObtenerAsync(Guid idCafeteria, CancellationToken cancellationToken = default);
    Task<CafeteriaDto> CrearAsync(GuardarCafeteriaDto dto, CancellationToken cancellationToken = default);
    Task<CafeteriaDto> ActualizarAsync(Guid idCafeteria, GuardarCafeteriaDto dto, CancellationToken cancellationToken = default);
    Task DesactivarAsync(Guid idCafeteria, CancellationToken cancellationToken = default);
    Task ReactivarAsync(Guid idCafeteria, CancellationToken cancellationToken = default);
}
