using CampusBite.Api.Common.Settings;
using CampusBite.Application.Auth;
using CampusBite.Application.Auth.Dtos;
using CampusBite.Application.Common.Security;
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

    // JJ-Sprint2 09/10/2026: registra un alumno y devuelve su token (inicia sesión de una vez)
    [HttpPost("registro")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitSettings.PoliticaAuth)]
    [ProducesResponseType<AuthResponseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResponseDto>> Registrar([FromBody] RegistroRequestDto dto, CancellationToken cancellationToken)
    {
        var respuesta = await _authService.RegistrarAsync(dto, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, respuesta);
    }

    // JJ-Sprint2 09/10/2026: inicia sesión; mismo 401 para correo inexistente o contraseña incorrecta
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitSettings.PoliticaAuth)]
    [ProducesResponseType<AuthResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginRequestDto dto, CancellationToken cancellationToken)
    {
        return Ok(await _authService.LoginAsync(dto, cancellationToken));
    }

    // JJ-Sprint1 02/10/2026: endpoint protegido para verificar que el token es válido
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public IActionResult Me()
    {
        // JY-Sprint2 10/10/2026: lee la sesión con IUsuarioActual (MapInboundClaims = false: claims "sub", "email", "role")
        return Ok(new
        {
            Id = _usuarioActual.Id,
            Correo = User.FindFirst(JwtRegisteredClaimNames.Email)?.Value,
            Rol = _usuarioActual.Rol.ToString()
        });
    }
}
