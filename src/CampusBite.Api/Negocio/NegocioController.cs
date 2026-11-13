using CampusBite.Api.Common.Security;
using CampusBite.Application.Common.Security;
using CampusBite.Application.Negocio;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampusBite.Api.Negocio;

// JJ-Sprint5 13/11/2026: panel del encargado: horario semanal, cerrar por hoy, promociones y estadísticas.
// GET api/promociones/activas (cualquier sesión) vive en MenuController: aquí la clase exige PersonalCafeteria.
[ApiController]
[Produces("application/json")]
[Authorize(Policy = PoliticasAutorizacion.PersonalCafeteria)]
public class NegocioController : ControllerBase
{
    private readonly INegocioRepository _negocio;
    private readonly IUsuarioActual _usuario;

    public NegocioController(INegocioRepository negocio, IUsuarioActual usuario)
    {
        _negocio = negocio;
        _usuario = usuario;
    }

    [HttpGet("api/cafeterias/{idCafeteria:guid}/horario")]
    [ProducesResponseType<HorarioDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HorarioDto>> Horario(Guid idCafeteria, CancellationToken cancellationToken) =>
        Ok(await _negocio.ObtenerHorarioAsync(idCafeteria, _usuario.Id, cancellationToken));

    // Reemplaza la semana completa (7 días, lunes primero)
    [HttpPut("api/cafeterias/{idCafeteria:guid}/horario")]
    [ProducesResponseType<HorarioDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HorarioDto>> GuardarHorario(Guid idCafeteria, [FromBody] GuardarHorarioDto dto, CancellationToken cancellationToken)
    {
        await _negocio.GuardarHorarioAsync(idCafeteria, _usuario.Id, dto.Dias, cancellationToken);
        return Ok(await _negocio.ObtenerHorarioAsync(idCafeteria, _usuario.Id, cancellationToken));
    }

    [HttpPost("api/cafeterias/{idCafeteria:guid}/horario/cierre-hoy")]
    [ProducesResponseType<HorarioDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<HorarioDto>> CerrarHoy(Guid idCafeteria, CancellationToken cancellationToken) =>
        CambiarCierreHoy(idCafeteria, true, cancellationToken);

    [HttpDelete("api/cafeterias/{idCafeteria:guid}/horario/cierre-hoy")]
    [ProducesResponseType<HorarioDto>(StatusCodes.Status200OK)]
    public Task<ActionResult<HorarioDto>> ReabrirHoy(Guid idCafeteria, CancellationToken cancellationToken) =>
        CambiarCierreHoy(idCafeteria, false, cancellationToken);

    private async Task<ActionResult<HorarioDto>> CambiarCierreHoy(Guid idCafeteria, bool cerrar, CancellationToken cancellationToken)
    {
        await _negocio.CambiarCierreHoyAsync(idCafeteria, _usuario.Id, cerrar, cancellationToken);
        return Ok(await _negocio.ObtenerHorarioAsync(idCafeteria, _usuario.Id, cancellationToken));
    }

    [HttpGet("api/cafeterias/{idCafeteria:guid}/promociones")]
    [ProducesResponseType<IReadOnlyList<PromocionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<PromocionDto>>> Promociones(Guid idCafeteria, CancellationToken cancellationToken) =>
        Ok(await _negocio.ListarPromocionesAsync(idCafeteria, _usuario.Id, cancellationToken));

    [HttpPost("api/cafeterias/{idCafeteria:guid}/promociones")]
    [ProducesResponseType<PromocionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PromocionDto>> CrearPromocion(Guid idCafeteria, [FromBody] GuardarPromocionDto dto, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created, await _negocio.GuardarPromocionAsync(idCafeteria, _usuario.Id, null, dto, cancellationToken));

    // Editar, pausar o activar
    [HttpPut("api/cafeterias/{idCafeteria:guid}/promociones/{idPromocion:guid}")]
    [ProducesResponseType<PromocionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PromocionDto>> ActualizarPromocion(
        Guid idCafeteria, Guid idPromocion, [FromBody] GuardarPromocionDto dto, CancellationToken cancellationToken) =>
        Ok(await _negocio.GuardarPromocionAsync(idCafeteria, _usuario.Id, idPromocion, dto, cancellationToken));

    [HttpDelete("api/cafeterias/{idCafeteria:guid}/promociones/{idPromocion:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EliminarPromocion(Guid idCafeteria, Guid idPromocion, CancellationToken cancellationToken)
    {
        await _negocio.EliminarPromocionAsync(idCafeteria, _usuario.Id, idPromocion, cancellationToken);
        return NoContent();
    }

    // periodo: hoy | 7d | 30d (contando hoy)
    [HttpGet("api/cafeterias/{idCafeteria:guid}/estadisticas")]
    [ProducesResponseType<EstadisticasDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EstadisticasDto>> Estadisticas(Guid idCafeteria, [FromQuery] string periodo = "hoy",
        CancellationToken cancellationToken = default)
    {
        if (periodo is not ("hoy" or "7d" or "30d"))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "BadRequest",
                detail: "El periodo debe ser hoy, 7d o 30d.");
        }
        return Ok(await _negocio.EstadisticasAsync(idCafeteria, _usuario.Id, periodo, cancellationToken));
    }
}
