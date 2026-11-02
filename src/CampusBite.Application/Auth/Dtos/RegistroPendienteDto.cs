namespace CampusBite.Application.Auth.Dtos;

// JY-Sprint4 31/10/2026: respuesta del registro (alumno o negocio). Ya no trae token: hay que verificar el correo
// antes de iniciar sesión. Si CorreoEnviado es false, la cuenta sí se creó y el usuario puede pedir que se reenvíe.
public class RegistroPendienteDto
{
    public required string Correo { get; init; }
    public required bool CorreoEnviado { get; init; }
    public required string Mensaje { get; init; }
}
