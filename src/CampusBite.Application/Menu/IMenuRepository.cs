using CampusBite.Application.Menu.Dtos;

namespace CampusBite.Application.Menu;

// JY-Sprint4 05/11/2026: HU-07B. Lectura del menú para el alumno; Infrastructure lo implementa con
// sp_menu_cafeteria (que ya existía desde Sprint 2 sin nadie que lo llamara).
// Sin idActor: el menú es el mismo para cualquier usuario con sesión, el procedimiento solo muestra
// cafeterías activas y productos disponibles.
public interface IMenuRepository
{
    Task<IReadOnlyList<MenuCafeteriaDto>> ListarAsync(CancellationToken cancellationToken = default);

    // null si la cafetería no existe o está inactiva
    Task<MenuCafeteriaDto?> ObtenerAsync(Guid idCafeteria, CancellationToken cancellationToken = default);
}
