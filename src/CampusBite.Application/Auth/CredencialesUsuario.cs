using CampusBite.Domain.Auth;

namespace CampusBite.Application.Auth;

// JJ-Sprint1 04/10/2026: datos para validar el login (los obtiene el repositorio con sp_obtener_credenciales)
// JJ-Sprint5 13/11/2026: + Nombre, IdCafeteria (respuesta de login y /me) y Correo (lectura por id, sp_obtener_usuario)
public class CredencialesUsuario
{
    public Guid IdUsuario { get; set; }
    public RolUsuario IdRol { get; set; }
    public string ContrasenaHash { get; set; } = string.Empty;
    public bool Activo { get; set; }

    // JY-Sprint4 31/10/2026: sin verificar no puede iniciar sesión
    public bool CorreoVerificado { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public Guid? IdCafeteria { get; set; }
    public string Correo { get; set; } = string.Empty;
}
