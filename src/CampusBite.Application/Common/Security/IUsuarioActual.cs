using CampusBite.Domain.Auth;

namespace CampusBite.Application.Common.Security;

// JY-Sprint2 10/10/2026: quién hace la petición, leído del JWT ya validado (lo implementa la capa Api).
// Los servicios lo usan para mandar p_id_usuario / p_id_actor a los procedimientos de la BD.
// REGLA: el id de quien actúa sale SIEMPRE de aquí, nunca del body, la query o la ruta; si no, cualquiera
// podría hacerse pasar por otro mandando un id ajeno.
public interface IUsuarioActual
{
    // Lanzan UnauthorizedException si no hay sesión o el token no trae un id o rol válidos
    Guid Id { get; }
    RolUsuario Rol { get; }
}
