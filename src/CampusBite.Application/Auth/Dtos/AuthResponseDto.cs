namespace CampusBite.Application.Auth.Dtos;

// JJ-Sprint1 04/10/2026: respuesta de login/registro con el token JWT
// JJ-Sprint5 13/11/2026: + Nombre e IdCafeteria (null para Alumno y AdminSistema)
public class AuthResponseDto
{
    public Guid UsuarioId { get; set; }
    public string Correo { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public Guid? IdCafeteria { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiraEn { get; set; }
}
