namespace CampusBite.Application.Common.Correo;

// JY-Sprint4 31/10/2026: envío de correos. Infrastructure lo implementa con SMTP (MailKit) y arma el enlace con la
// URL del frontend. Si el servidor de correo no responde o no está configurado lanza ServiceUnavailableException.
public interface IEnviadorCorreo
{
    Task EnviarVerificacionAsync(string correo, string nombre, string token, CancellationToken cancellationToken = default);
}
