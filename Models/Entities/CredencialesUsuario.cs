using CampusBite_Back.Models.Enums;

namespace CampusBite_Back.Models.Entities;

// JJ-Sprint1 04/10/2026: fila que devuelve sp_obtener_credenciales (tabla usuario de db/migrations/V001)
public class CredencialesUsuario
{
    public Guid IdUsuario { get; set; }
    public RolUsuario IdRol { get; set; }
    public string ContrasenaHash { get; set; } = string.Empty;
    public bool Activo { get; set; }
}
