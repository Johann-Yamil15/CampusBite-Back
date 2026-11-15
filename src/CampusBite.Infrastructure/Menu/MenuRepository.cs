using CampusBite.Application.Menu;
using CampusBite.Application.Menu.Dtos;
using CampusBite.Infrastructure.Persistence;
using static CampusBite.Infrastructure.Persistence.Procedimientos;

namespace CampusBite.Infrastructure.Menu;

// JY-Sprint4 05/11/2026: HU-07B. Implementa IMenuRepository con sp_menu_cafeteria.
// El procedimiento siempre devuelve un arreglo: con NULL son todas las cafeterías activas y con un id es
// ese arreglo con cero o un elemento.
public class MenuRepository : IMenuRepository
{
    private readonly AppDbContext _context;

    public MenuRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<IReadOnlyList<MenuCafeteriaDto>> ListarAsync(CancellationToken cancellationToken = default) =>
        _context.EjecutarAsync<IReadOnlyList<MenuCafeteriaDto>>(
            "CALL sp_menu_cafeteria(NULL, NULL)",
            [],
            fila => fila.LeerJson<List<MenuCafeteriaDto>>(0) ?? [],
            cancellationToken);

    public async Task<MenuCafeteriaDto?> ObtenerAsync(Guid idCafeteria, CancellationToken cancellationToken = default)
    {
        var menu = await _context.EjecutarAsync(
            "CALL sp_menu_cafeteria(@id, NULL)",
            [Id("id", idCafeteria)],
            fila => fila.LeerJson<List<MenuCafeteriaDto>>(0),
            cancellationToken);

        return menu?.FirstOrDefault();
    }
}
