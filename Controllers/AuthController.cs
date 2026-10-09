using System.Security.Claims;
using CampusBite_Back.Common.Settings;
using CampusBite_Back.Interfaces;
using CampusBite_Back.Models.Dtos.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CampusBite_Back.Controllers;

// JJ-Sprint2 09/10/2026: endpoints de autenticación; delega la lógica a IAuthService y documenta sus respuestas en Swagger
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    // JJ-Sprint2 09/10/2026: registra un alumno y devuelve su token (inicia sesión de una vez)
    [HttpPost("registro")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitSettings.PoliticaAuth)]
    [ProducesResponseType<AuthResponseDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResponseDto>> Registrar([FromBody] RegistroRequestDto dto)
    {
        var respuesta = await _authService.RegistrarAsync(dto);
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
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginRequestDto dto)
    {
        return Ok(await _authService.LoginAsync(dto));
    }

    // JJ-Sprint1 02/10/2026: endpoint protegido para verificar que el token es válido
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Me()
    {
        return Ok(new
        {
            Id = User.FindFirstValue(ClaimTypes.NameIdentifier),
            Correo = User.FindFirstValue(ClaimTypes.Email),
            Rol = User.FindFirstValue(ClaimTypes.Role)
        });
    }
}
