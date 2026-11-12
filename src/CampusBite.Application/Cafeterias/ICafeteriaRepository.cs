using CampusBite.Application.Cafeterias.Dtos;

namespace CampusBite.Application.Cafeterias;

// JY-Sprint3 20/10/2026: TEC-08. Acceso a cafeterías; Infrastructure lo implementa con los sp_*_cafeteria.
// idActor siempre es el usuario de la sesión (IUsuarioActual): la BD valida con él los permisos.
// Errores: NotFoundException, ForbiddenException, ConflictException (nombre repetido u órdenes en curso)
// y BusinessRuleException (datos inválidos).
public interface ICafeteriaRepository
{
    // JY-Sprint4 31/10/2026: una página por nombre y el total que cumple el filtro (buscar null = todas)
    Task<(int Total, IReadOnlyList<CafeteriaDto> Items)> ListarAsync(
        Guid idActor, bool incluirInactivas, string? buscar, int limite, int desplazamiento,
        CancellationToken cancellationToken = default);

    // null si no existe o si es inactiva y el actor no la administra
    Task<CafeteriaDto?> ObtenerAsync(Guid idCafeteria, Guid idActor, CancellationToken cancellationToken = default);

    Task<Guid> CrearAsync(Guid idActor, DatosCafeteria datos, CancellationToken cancellationToken = default);

    Task ActualizarAsync(Guid idCafeteria, Guid idActor, DatosCafeteria datos, CancellationToken cancellationToken = default);

    Task CambiarActivaAsync(Guid idCafeteria, bool activa, Guid idActor, CancellationToken cancellationToken = default);
}
