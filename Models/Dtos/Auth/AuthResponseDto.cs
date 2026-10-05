namespace CampusBite_Back.Models.Dtos.Auth;

// JJ-Sprint1 04/10/2026: respuesta de login/registro con el token JWT
public class AuthResponseDto
{
    public Guid UsuarioId { get; set; }
    public string Correo { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiraEn { get; set; }
}
