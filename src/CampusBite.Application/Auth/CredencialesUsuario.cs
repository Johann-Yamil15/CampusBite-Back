using CampusBite.Domain.Auth;

namespace CampusBite.Application.Auth;

// JJ-Sprint1 04/10/2026: datos para validar el login (los obtiene el repositorio con sp_obtener_credenciales)
public class CredencialesUsuario
{
    public Guid IdUsuario { get; set; }
    public RolUsuario IdRol { get; set; }
    public string ContrasenaHash { get; set; } = string.Empty;
    public bool Activo { get; set; }
}
