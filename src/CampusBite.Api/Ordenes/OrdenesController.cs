using CampusBite.Api.Common.Security;
using CampusBite.Application.Common;
using CampusBite.Application.Common.Security;
using CampusBite.Application.Ordenes;
using CampusBite.Application.Ordenes.Dtos;
using CampusBite.Domain.Ordenes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampusBite.Api.Ordenes;

// JJ-Sprint5 13/11/2026: órdenes del alumno (checkout, mis pedidos, detalle en vivo, cancelar, reportar) y tablero
// de la cafetería (cola del día, cambiar estado, entregar con código, rechazar, confirmar pago).
// Las acciones responden la orden actualizada (OrdenDto) leída otra vez de la BD.
[ApiController]
[Produces("application/json")]
[Authorize]
public class OrdenesController : ControllerBase
{
    private readonly IOrdenRepository _ordenes;
    private readonly IUsuarioActual _usuario;

    public OrdenesController(IOrdenRepository ordenes, IUsuarioActual usuario)
    {
        _ordenes = ordenes;
        _usuario = usuario;
    }

    [HttpPost("api/ordenes")]
    [Authorize(Policy = PoliticasAutorizacion.SoloAlumno)]
    [ProducesResponseType<OrdenDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OrdenDto>> Crear([FromBody] CrearOrdenDto dto, CancellationToken cancellationToken)
    {
        var id = await _ordenes.CrearAsync(_usuario.Id, dto.IdCafeteria!.Value, dto.RecogidaEn!.Value, dto.Metodo!.Value,
            dto.Items, string.IsNullOrWhiteSpace(dto.Notas) ? null : dto.Notas.Trim(), cancellationToken);
        var orden = await _ordenes.ObtenerAsync(id, _usuario.Id, cancellationToken);
        return CreatedAtRoute(nameof(ObtenerOrden), new { idOrden = id }, orden);
    }

    [HttpGet("api/ordenes/mias")]
    [Authorize(Policy = PoliticasAutorizacion.SoloAlumno)]
    [ProducesResponseType<PaginaDto<OrdenDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaginaDto<OrdenDto>>> Mias(
        [FromQuery] int pagina = 1, [FromQuery] int tamanoPagina = 20, CancellationToken cancellationToken = default)
    {
        return Ok(await _ordenes.MisOrdenesAsync(_usuario.Id, pagina, tamanoPagina, cancellationToken));
    }

    // El alumno dueño (con código) o el personal de esa cafetería (sin código). El front consulta cada 5 s
    [HttpGet("api/ordenes/{idOrden:guid}", Name = nameof(ObtenerOrden))]
    [ProducesResponseType<OrdenDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrdenDto>> ObtenerOrden(Guid idOrden, CancellationToken cancellationToken)
    {
        return Ok(await _ordenes.ObtenerAsync(idOrden, _usuario.Id, cancellationToken));
    }

    [HttpPost("api/ordenes/{idOrden:guid}/cancelar")]
    [Authorize(Policy = PoliticasAutorizacion.SoloAlumno)]
    [ProducesResponseType<OrdenDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrdenDto>> Cancelar(Guid idOrden, CancellationToken cancellationToken)
    {
        await _ordenes.CancelarAsync(idOrden, _usuario.Id, cancellationToken);
        return Ok(await _ordenes.ObtenerAsync(idOrden, _usuario.Id, cancellationToken));
    }

    [HttpPost("api/ordenes/{idOrden:guid}/reportes")]
    [Authorize(Policy = PoliticasAutorizacion.SoloAlumno)]
    [ProducesResponseType<ReporteCreadoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReporteCreadoDto>> Reportar(Guid idOrden, [FromBody] CrearReporteDto dto, CancellationToken cancellationToken)
    {
        var folio = await _ordenes.ReportarAsync(idOrden, _usuario.Id, dto.Motivo!.Value,
            string.IsNullOrWhiteSpace(dto.Detalle) ? null : dto.Detalle.Trim(), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new ReporteCreadoDto(folio));
    }

    // Tablero del día (por omisión hoy en America/Mexico_City). El front consulta cada 10 s
    [HttpGet("api/cafeterias/{idCafeteria:guid}/ordenes")]
    [Authorize(Policy = PoliticasAutorizacion.PersonalCafeteria)]
    [ProducesResponseType<IReadOnlyList<OrdenDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<OrdenDto>>> Tablero(
        Guid idCafeteria, [FromQuery] DateOnly? fecha, CancellationToken cancellationToken)
    {
        return Ok(await _ordenes.TableroAsync(idCafeteria, _usuario.Id, fecha, cancellationToken));
    }

    // Solo EnPreparacion o Lista: cancelar es /rechazar (con motivo) y entregar es /entregar (con código)
    [HttpPatch("api/ordenes/{idOrden:guid}/estado")]
    [Authorize(Policy = PoliticasAutorizacion.PersonalCafeteria)]
    [ProducesResponseType<OrdenDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrdenDto>> CambiarEstado(Guid idOrden, [FromBody] CambiarEstadoOrdenDto dto, CancellationToken cancellationToken)
    {
        if (dto.Estado is not (EstadoOrden.EnPreparacion or EstadoOrden.Lista))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "BadRequest",
                detail: "Solo se puede cambiar a EnPreparacion o Lista.");
        }

        await _ordenes.CambiarEstadoAsync(idOrden, dto.Estado.Value, _usuario.Id, null, cancellationToken);
        return Ok(await _ordenes.ObtenerAsync(idOrden, _usuario.Id, cancellationToken));
    }

    [HttpPost("api/ordenes/{idOrden:guid}/entregar")]
    [Authorize(Policy = PoliticasAutorizacion.PersonalCafeteria)]
    [ProducesResponseType<OrdenDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<OrdenDto>> Entregar(Guid idOrden, [FromBody] EntregarOrdenDto dto, CancellationToken cancellationToken)
    {
        await _ordenes.EntregarAsync(idOrden, dto.CodigoRecogida, _usuario.Id, cancellationToken);
        return Ok(await _ordenes.ObtenerAsync(idOrden, _usuario.Id, cancellationToken));
    }

    [HttpPost("api/ordenes/{idOrden:guid}/rechazar")]
    [Authorize(Policy = PoliticasAutorizacion.PersonalCafeteria)]
    [ProducesResponseType<OrdenDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrdenDto>> Rechazar(Guid idOrden, [FromBody] RechazarOrdenDto dto, CancellationToken cancellationToken)
    {
        await _ordenes.CambiarEstadoAsync(idOrden, EstadoOrden.Cancelada, _usuario.Id, dto.Motivo.Trim(), cancellationToken);
        return Ok(await _ordenes.ObtenerAsync(idOrden, _usuario.Id, cancellationToken));
    }

    [HttpPost("api/ordenes/{idOrden:guid}/confirmar-pago")]
    [Authorize(Policy = PoliticasAutorizacion.PersonalCafeteria)]
    [ProducesResponseType<OrdenDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrdenDto>> ConfirmarPago(Guid idOrden, [FromBody] ConfirmarPagoDto? dto, CancellationToken cancellationToken)
    {
        await _ordenes.ConfirmarPagoAsync(idOrden, _usuario.Id, dto?.Referencia, cancellationToken);
        return Ok(await _ordenes.ObtenerAsync(idOrden, _usuario.Id, cancellationToken));
    }
}
