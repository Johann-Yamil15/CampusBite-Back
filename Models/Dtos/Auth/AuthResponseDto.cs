namespace CampusBite_Back.Models.Dtos.Auth;

// JJ-Sprint1 02/10/2026: respuesta de login/registro con el token JWT
public class AuthResponseDto
{
    public int UsuarioId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiraEn { get; set; }
}
