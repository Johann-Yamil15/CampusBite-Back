using CampusBite.Api.Common.Security;
using CampusBite.Application.Cafeterias;
using CampusBite.Application.Cafeterias.Dtos;
using CampusBite.Application.Common.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampusBite.Api.Cafeterias;

// JY-Sprint3 20/10/2026: TEC-08. CRUD de cafeterías. Sin lógica: valida el DTO, llama al servicio y responde.
// Permisos: la política de cada acción es la primera barrera; la BD vuelve a revisar con el usuario de la sesión
// (p. ej. que un encargado solo edite SU cafetería).
// "Eliminar" es una baja lógica: la cafetería deja de aparecer y de aceptar órdenes, pero su historial se conserva.
[ApiController]
[Route("api/cafeterias")]
[Produces("application/json")]
[Authorize]
public class CafeteriasController : ControllerBase
{
    private readonly ICafeteriaService _cafeterias;

    public CafeteriasController(ICafeteriaService cafeterias)
    {
        _cafeterias = cafeterias;
    }

    // Paginado (?pagina=1&tamano=20, máximo 100) y con búsqueda por nombre (?buscar=norte).
    // Cualquier usuario con sesión ve las activas; incluirInactivas=true solo admin del sistema
    [HttpGet]
    [ProducesResponseType<PaginaDto<CafeteriaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PaginaDto<CafeteriaDto>>> Listar(
        [FromQuery] ListarCafeteriasDto filtro, CancellationToken cancellationToken)
    {
        return Ok(await _cafeterias.ListarAsync(filtro, cancellationToken));
    }

    // Una inactiva solo la ven su encargado y el admin del sistema; para los demás es 404
    [HttpGet("{idCafeteria:guid}", Name = nameof(Obtener))]
    [ProducesResponseType<CafeteriaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CafeteriaDto>> Obtener(Guid idCafeteria, CancellationToken cancellationToken)
    {
        return Ok(await _cafeterias.ObtenerAsync(idCafeteria, cancellationToken));
    }

    [HttpPost]
    [Authorize(Policy = PoliticasAutorizacion.SoloAdminSistema)]
    [ProducesResponseType<CafeteriaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CafeteriaDto>> Crear([FromBody] GuardarCafeteriaDto dto, CancellationToken cancellationToken)
    {
        var creada = await _cafeterias.CrearAsync(dto, cancellationToken);
        return CreatedAtRoute(nameof(Obtener), new { idCafeteria = creada.IdCafeteria }, creada);
    }

    // Su encargado o el admin del sistema. Reemplaza los datos y los métodos de pago
    [HttpPut("{idCafeteria:guid}")]
    [Authorize(Policy = PoliticasAutorizacion.PersonalCafeteria)]
    [ProducesResponseType<CafeteriaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CafeteriaDto>> Actualizar(
        Guid idCafeteria, [FromBody] GuardarCafeteriaDto dto, CancellationToken cancellationToken)
    {
        return Ok(await _cafeterias.ActualizarAsync(idCafeteria, dto, cancellationToken));
    }

    // Baja lógica. 409 si tiene órdenes en curso. Repetirla no es error (sigue inactiva)
    [HttpDelete("{idCafeteria:guid}")]
    [Authorize(Policy = PoliticasAutorizacion.SoloAdminSistema)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Desactivar(Guid idCafeteria, CancellationToken cancellationToken)
    {
        await _cafeterias.DesactivarAsync(idCafeteria, cancellationToken);
        return NoContent();
    }

    [HttpPost("{idCafeteria:guid}/reactivar")]
    [Authorize(Policy = PoliticasAutorizacion.SoloAdminSistema)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reactivar(Guid idCafeteria, CancellationToken cancellationToken)
    {
        await _cafeterias.ReactivarAsync(idCafeteria, cancellationToken);
        return NoContent();
    }
}
