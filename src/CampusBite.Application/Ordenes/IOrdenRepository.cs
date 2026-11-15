using CampusBite.Application.Common;
using CampusBite.Application.Ordenes.Dtos;
using CampusBite.Domain.Cafeterias;
using CampusBite.Domain.Ordenes;

namespace CampusBite.Application.Ordenes;

// JJ-Sprint5 13/11/2026: órdenes del alumno y tablero de la cafetería, sobre los sp_* de órdenes.
// Los permisos (dueño de la orden / encargado de esa cafetería) los vuelve a revisar la BD con idActor.
// shortcut: sin capa de servicio (como Menu): no hay reglas fuera de los procedimientos.
public interface IOrdenRepository
{
    Task<Guid> CrearAsync(Guid idAlumno, Guid idCafeteria, DateTimeOffset recogidaEn, MetodoPago metodo,
        IReadOnlyList<ItemOrdenDto> items, string? notas, CancellationToken cancellationToken = default);

    Task<PaginaDto<OrdenDto>> MisOrdenesAsync(Guid idAlumno, int pagina, int tamanoPagina, CancellationToken cancellationToken = default);

    // 404 si no existe; 403 si no es suya ni de su cafetería
    Task<OrdenDto> ObtenerAsync(Guid idOrden, Guid idActor, CancellationToken cancellationToken = default);

    Task CancelarAsync(Guid idOrden, Guid idAlumno, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrdenDto>> TableroAsync(Guid idCafeteria, Guid idActor, DateOnly? fecha, CancellationToken cancellationToken = default);

    Task CambiarEstadoAsync(Guid idOrden, EstadoOrden estado, Guid idActor, string? motivo, CancellationToken cancellationToken = default);

    // Código erróneo -> BusinessRuleException (400); 5 errores seguidos -> TooManyRequestsException (429) por 10 min
    Task EntregarAsync(Guid idOrden, string codigo, Guid idActor, CancellationToken cancellationToken = default);

    Task ConfirmarPagoAsync(Guid idOrden, Guid idActor, string? referencia, CancellationToken cancellationToken = default);

    Task<string> ReportarAsync(Guid idOrden, Guid idAlumno, MotivoReporte motivo, string? detalle, CancellationToken cancellationToken = default);
}
