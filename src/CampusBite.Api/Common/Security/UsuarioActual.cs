using CampusBite.Application.Common.Security;
using CampusBite.Domain.Auth;
using CampusBite.Domain.Common.Exceptions;
using CampusBite.Infrastructure.Security;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CampusBite.Api.Common.Security;

// JY-Sprint2 10/10/2026: implementa IUsuarioActual con los claims del token de la petición en curso
public class UsuarioActual : IUsuarioActual
{
    private readonly IHttpContextAccessor _accessor;

    public UsuarioActual(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public Guid Id =>
        Guid.TryParse(Claim(JwtRegisteredClaimNames.Sub), out var id)
            ? id
            : throw new UnauthorizedException("La sesión no es válida. Inicia sesión de nuevo.");

    public RolUsuario Rol =>
        Enum.TryParse<RolUsuario>(Claim(TokenService.ClaimRol), ignoreCase: false, out var rol) && Enum.IsDefined(rol)
            ? rol
            : throw new UnauthorizedException("La sesión no es válida. Inicia sesión de nuevo.");

    private string? Claim(string tipo)
    {
        var usuario = _accessor.HttpContext?.User;
        return usuario?.Identity?.IsAuthenticated == true ? usuario.FindFirst(tipo)?.Value : null;
    }
}
