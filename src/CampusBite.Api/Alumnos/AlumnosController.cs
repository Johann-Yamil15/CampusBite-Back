using CampusBite.Api.Common.Security;
using CampusBite.Application.Alumnos;
using CampusBite.Application.Common.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampusBite.Api.Alumnos;

// JJ-Sprint5 13/11/2026: perfil, preferencias y favoritos del alumno de la sesión (siempre "me": el id sale del token)
[ApiController]
[Produces("application/json")]
[Authorize(Policy = PoliticasAutorizacion.SoloAlumno)]
public class AlumnosController : ControllerBase
{
    private readonly IAlumnoRepository _alumnos;
    private readonly IUsuarioActual _usuario;

    public AlumnosController(IAlumnoRepository alumnos, IUsuarioActual usuario)
    {
        _alumnos = alumnos;
        _usuario = usuario;
    }

    [HttpGet("api/alumnos/me/perfil")]
    [ProducesResponseType<PerfilAlumnoDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PerfilAlumnoDto>> Perfil(CancellationToken cancellationToken) =>
        Ok(await _alumnos.ObtenerPerfilAsync(_usuario.Id, cancellationToken));

    [HttpPut("api/alumnos/me/perfil")]
    [ProducesResponseType<DatosAlumnoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DatosAlumnoDto>> ActualizarPerfil([FromBody] DatosAlumnoDto dto, CancellationToken cancellationToken)
    {
        await _alumnos.ActualizarDatosAsync(_usuario.Id, dto, cancellationToken);
        return Ok((await _alumnos.ObtenerPerfilAsync(_usuario.Id, cancellationToken)).Datos);
    }

    [HttpPut("api/alumnos/me/preferencias")]
    [ProducesResponseType<PreferenciasAlumnoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PreferenciasAlumnoDto>> GuardarPreferencias([FromBody] PreferenciasAlumnoDto dto, CancellationToken cancellationToken)
    {
        await _alumnos.GuardarPreferenciasAsync(_usuario.Id, dto, cancellationToken);
        return Ok(dto);
    }

    [HttpGet("api/favoritos/cafeterias")]
    [ProducesResponseType<IReadOnlyList<FavoritoCafeteriaDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FavoritoCafeteriaDto>>> FavoritosCafeterias(CancellationToken cancellationToken) =>
        Ok(await _alumnos.FavoritosCafeteriasAsync(_usuario.Id, cancellationToken));

    // Idempotente: si ya estaba, 200 igual
    [HttpPut("api/favoritos/cafeterias/{idCafeteria:guid}")]
    [ProducesResponseType<FavoritoCafeteriaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FavoritoCafeteriaDto>> GuardarFavoritoCafeteria(Guid idCafeteria, CancellationToken cancellationToken) =>
        Ok(await _alumnos.GuardarFavoritoCafeteriaAsync(_usuario.Id, idCafeteria, cancellationToken));

    [HttpDelete("api/favoritos/cafeterias/{idCafeteria:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> QuitarFavoritoCafeteria(Guid idCafeteria, CancellationToken cancellationToken)
    {
        await _alumnos.QuitarFavoritoCafeteriaAsync(_usuario.Id, idCafeteria, cancellationToken);
        return NoContent();
    }

    [HttpGet("api/favoritos/platillos")]
    [ProducesResponseType<IReadOnlyList<FavoritoPlatilloDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FavoritoPlatilloDto>>> FavoritosPlatillos(CancellationToken cancellationToken) =>
        Ok(await _alumnos.FavoritosPlatillosAsync(_usuario.Id, cancellationToken));

    [HttpPut("api/favoritos/platillos/{idProducto:guid}")]
    [ProducesResponseType<FavoritoPlatilloDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FavoritoPlatilloDto>> GuardarFavoritoPlatillo(Guid idProducto, CancellationToken cancellationToken) =>
        Ok(await _alumnos.GuardarFavoritoPlatilloAsync(_usuario.Id, idProducto, cancellationToken));

    [HttpDelete("api/favoritos/platillos/{idProducto:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> QuitarFavoritoPlatillo(Guid idProducto, CancellationToken cancellationToken)
    {
        await _alumnos.QuitarFavoritoPlatilloAsync(_usuario.Id, idProducto, cancellationToken);
        return NoContent();
    }
}
