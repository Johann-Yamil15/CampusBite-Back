using CampusBite_Back.Models.Enums;

namespace CampusBite_Back.Models.Entities;

// JJ-Sprint1 02/10/2026: usuario del sistema usado para la autenticación JWT
public class Usuario : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public RolUsuario Rol { get; set; } = RolUsuario.Estudiante;
    public bool Activo { get; set; } = true;
}
