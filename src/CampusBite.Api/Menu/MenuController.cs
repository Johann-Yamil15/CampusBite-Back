using CampusBite.Application.Common;
using CampusBite.Application.Menu;
using CampusBite.Application.Menu.Dtos;
using CampusBite.Application.Negocio;
using CampusBite.Domain.Common.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampusBite.Api.Menu;

// JY-Sprint4 05/11/2026: HU-07B. Menú para el alumno: solo lectura, cualquier usuario con sesión.
// Es la única forma en que un alumno ve productos: GET api/cafeterias/{id}/productos es del personal de
// la cafetería (incluye los no disponibles y datos de gestión).
// shortcut: sin capa de servicio, no hay reglas que aplicar sobre el procedimiento; si HU-08 agrega lógica
// (favoritos, disponibilidad por horario) se mete un IMenuService como en Cafeterias.
[ApiController]
[Route("api/menu")]
[Produces("application/json")]
[Authorize]
public class MenuController : ControllerBase
{
    private readonly IMenuRepository _menu;
    private readonly INegocioRepository _negocio;

    public MenuController(IMenuRepository menu, INegocioRepository negocio)
    {
        _menu = menu;
        _negocio = negocio;
    }

    // JJ-Sprint5 13/11/2026: carrusel del inicio y franja de la tienda (activas y vigentes hoy). Ruta absoluta
    [HttpGet("/api/promociones/activas")]
    [ProducesResponseType<IReadOnlyList<PromocionActivaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<PromocionActivaDto>>> PromocionesActivas(CancellationToken cancellationToken)
    {
        return Ok(await _negocio.PromocionesActivasAsync(cancellationToken));
    }

    // Menú de todas las cafeterías activas
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MenuCafeteriaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<MenuCafeteriaDto>>> Listar(CancellationToken cancellationToken)
    {
        return Ok(await _menu.ListarAsync(cancellationToken));
    }

    // JJ-Sprint5 13/11/2026: búsqueda paginada (P2); el front hoy filtra localmente con GET api/menu
    [HttpGet("buscar")]
    [ProducesResponseType<PaginaDto<PlatilloCatalogoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PaginaDto<PlatilloCatalogoDto>>> Buscar(
        [FromQuery] BuscarMenuQuery query, CancellationToken cancellationToken)
    {
        return Ok(BuscadorMenu.Buscar(await _menu.ListarAsync(cancellationToken), query));
    }

    // Una cafetería inactiva o inexistente es 404 por igual: el alumno no distingue entre las dos
    [HttpGet("{idCafeteria:guid}")]
    [ProducesResponseType<MenuCafeteriaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MenuCafeteriaDto>> Obtener(Guid idCafeteria, CancellationToken cancellationToken)
    {
        return Ok(await _menu.ObtenerAsync(idCafeteria, cancellationToken)
                  ?? throw new NotFoundException("La cafetería no existe o no está disponible."));
    }
}
