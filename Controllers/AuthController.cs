using System.Security.Claims;
using CampusBite_Back.Interfaces;
using CampusBite_Back.Models.Dtos.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampusBite_Back.Controllers;

// JJ-Sprint1 02/10/2026: endpoints de autenticación; delega la lógica a IAuthService
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("registro")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponseDto>> Registrar([FromBody] RegistroRequestDto dto)
    {
        var respuesta = await _authService.RegistrarAsync(dto);
        return StatusCode(StatusCodes.Status201Created, respuesta);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponseDto>> Login([FromBody] LoginRequestDto dto)
    {
        return Ok(await _authService.LoginAsync(dto));
    }

    // JJ-Sprint1 02/10/2026: endpoint protegido para verificar que el token es válido
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        return Ok(new
        {
            Id = User.FindFirstValue(ClaimTypes.NameIdentifier),
            Nombre = User.FindFirstValue(ClaimTypes.Name),
            Correo = User.FindFirstValue(ClaimTypes.Email),
            Rol = User.FindFirstValue(ClaimTypes.Role)
        });
    }
}
