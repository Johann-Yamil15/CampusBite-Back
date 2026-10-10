using CampusBite.Application.Common.Security;
using CampusBite.Domain.Auth;
using Microsoft.AspNetCore.Authorization;

namespace CampusBite.Api.Common.Security;

// JY-Sprint2 10/10/2026: TEC-07. Protección de rutas por rol con políticas de ASP.NET Core.
// Uso en un controlador o acción:  [Authorize(Policy = PoliticasAutorizacion.SoloAlumno)]
//
// - Denegar por defecto: la FallbackPolicy exige sesión en TODO endpoint que no diga otra cosa. Un endpoint
//   nuevo sin [Authorize] queda protegido, no público. Lo público se marca a propósito con [AllowAnonymous].
// - El rol del token es la primera barrera. La segunda está en la BD: los procedimientos vuelven a revisar rol,
//   que el usuario siga activo y que sea dueño o encargado del recurso (p. ej. sp_exigir_gestor_cafeteria).
//   Si la BD niega (42501), el repositorio debe lanzar ForbiddenException -> 403.
// - Los nombres de rol salen del enum RolUsuario, el mismo valor que TokenService escribe en el claim "role".
public static class PoliticasAutorizacion
{
    public const string SoloAlumno = nameof(SoloAlumno);                 // hace pedidos
    public const string PersonalCafeteria = nameof(PersonalCafeteria);   // atiende pedidos: encargado o admin del sistema
    public const string SoloAdminSistema = nameof(SoloAdminSistema);     // administra el sistema

    public static IServiceCollection AddAutorizacionPorRol(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IUsuarioActual, UsuarioActual>();

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(SoloAlumno, politica => politica
                .RequireAuthenticatedUser()
                .RequireRole(nameof(RolUsuario.Alumno)))
            .AddPolicy(PersonalCafeteria, politica => politica
                .RequireAuthenticatedUser()
                .RequireRole(nameof(RolUsuario.AdminCafeteria), nameof(RolUsuario.AdminSistema)))
            .AddPolicy(SoloAdminSistema, politica => politica
                .RequireAuthenticatedUser()
                .RequireRole(nameof(RolUsuario.AdminSistema)));

        return services;
    }
}
