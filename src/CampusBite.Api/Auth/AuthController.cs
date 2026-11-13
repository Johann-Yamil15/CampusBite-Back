using CampusBite.Api.Common.Settings;
using CampusBite.Application.Auth;
using CampusBite.Application.Auth.Dtos;
using CampusBite.Application.Common.Security;
using CampusBite.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CampusBite.Api.Auth;

// JJ-Sprint2 09/10/2026: endpoints de autenticación; delega la lógica a IAuthService y documenta sus respuestas en Swagger
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUsuarioActual _usuarioActual;

    public AuthController(IAuthService authService, IUsuarioActual usuarioActual)
    {
        _authService = authService;
        _usuarioActual = usuarioActual;
    }

    // JJ-Sprint2 09/10/2026: registra un alumno.
    // JY-Sprint4 31/10/2026: ya no inicia sesión; manda el correo de verificación
    [HttpPost("registro")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitSettings.PoliticaAuth)]
    [ProducesResponseType<RegistroPendienteDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<RegistroPendienteDto>> Registrar([FromBody] RegistroRequestDto dto, CancellationToken cancellationToken)
    {
        var respuesta = await _authService.RegistrarAsync(dto, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, respuesta);
    }

    // JJ-Sprint3 22/10/2026: TEC-11. Autorregistro público de un negocio: crea al encargado (AdminCafeteria) y su
    // cafetería. La autorización real está en la BD (sp_autorregistrar_cafeteria).
    // JY-Sprint4 31/10/2026: igual que /registro, manda el correo de verificación en lugar de iniciar sesión
    [HttpPost("registro-cafeteria")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitSettings.PoliticaAuth)]
    [ProducesResponseType<RegistroPendienteDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<RegistroPendienteDto>> RegistrarCafeteria(
        [FromBody] RegistroCafeteriaRequestDto dto, CancellationToken cancellationToken)
    {
        var respuesta = await _authService.RegistrarCafeteriaAsync(dto, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, respuesta);
    }

    // JY-Sprint4 31/10/2026: el frontend manda el token del enlace del correo; si sirve, inicia la sesión
    [HttpPost("verificar-correo")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitSettings.PoliticaAuth)]
    [ProducesResponseType<AuthResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResponseDto>> VerificarCorreo(
        [FromBody] VerificarCorreoRequestDto dto, CancellationToken cancellationToken)
    {
        return Ok(await _authService.VerificarCorreoAsync(dto, cancellationToken));
    }

    // JY-Sprint4 31/10/2026: 202 siempre, exista o no la cuenta, para no revelar qué correos están registrados
    [HttpPost("reenviar-verificacion")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitSettings.PoliticaAuth)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ReenviarVerificacion(
        [FromBody] ReenviarVerificacionRequestDto dto, CancellationToken cancellationToken)
    {
        await _authService.ReenviarVerificacionAsync(dto, cancellationToken);
        return Accepted(new { mensaje = "Si la cuenta existe y falta verificarla, te enviamos un correo nuevo." });
    }

    // JJ-Sprint3 21/10/2026: TEC-12. Recuperación simple (sin código de verificación); 200 siempre,
    // exista o no el correo, para no revelar qué cuentas están registradas
    [HttpPost("restablecer-password")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitSettings.PoliticaAuth)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RestablecerPassword([FromBody] RestablecerPasswordRequestDto dto, CancellationToken cancellationToken)
    {
        await _authService.RestablecerPasswordAsync(dto, cancellationToken);
        return Ok(new { mensaje = "Si el correo está registrado, su contraseña ya se actualizó." });
    }

    // JJ-Sprint2 09/10/2026: inicia sesión; mismo 401 para correo inexistente o contraseña incorrecta.
    // JY-Sprint4 31/10/2026: 403 si la contraseña es correcta pero falta verificar el correo
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitSettings.PoliticaAuth)]
    [ProducesResponseType<AuthResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginRequestDto dto, CancellationToken cancellationToken)
    {
        return Ok(await _authService.LoginAsync(dto, cancellationToken));
    }

    // JJ-Sprint1 02/10/2026: endpoint protegido para verificar que el token es válido
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<MeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public ActionResult<MeDto> Me()
    {
        // JY-Sprint2 10/10/2026: lee la sesión con IUsuarioActual (MapInboundClaims = false: claims "sub", "email", "role")
        // JJ-Sprint5 13/11/2026: + nombre e idCafeteria de sus claims (tras editar el perfil, el nombre se actualiza al
        // volver a iniciar sesión; PUT api/alumnos/me/perfil ya devuelve el nombre nuevo)
        return Ok(new MeDto
        {
            Id = _usuarioActual.Id,
            Correo = User.FindFirst(JwtRegisteredClaimNames.Email)?.Value ?? string.Empty,
            Rol = _usuarioActual.Rol.ToString(),
            Nombre = User.FindFirst(TokenService.ClaimNombre)?.Value ?? string.Empty,
            IdCafeteria = Guid.TryParse(User.FindFirst(TokenService.ClaimIdCafeteria)?.Value, out var caf) ? caf : null
        });
    }

    // JJ-Sprint5 13/11/2026: cambiar contraseña con la sesión iniciada (cualquier rol)
    [HttpPost("cambiar-password")]
    [Authorize]
    [EnableRateLimiting(RateLimitSettings.PoliticaAuth)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> CambiarPassword([FromBody] CambiarPasswordRequestDto dto, CancellationToken cancellationToken)
    {
        await _authService.CambiarPasswordAsync(_usuarioActual.Id, dto, cancellationToken);
        return NoContent();
    }
}
