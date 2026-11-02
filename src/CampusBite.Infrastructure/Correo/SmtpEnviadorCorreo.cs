using System.Net;
using CampusBite.Application.Common.Correo;
using CampusBite.Domain.Common.Exceptions;
using CampusBite.Infrastructure.Seguridad;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace CampusBite.Infrastructure.Correo;

// JY-Sprint4 31/10/2026: implementa IEnviadorCorreo con SMTP (MailKit). Cualquier falla del servidor de correo
// (sin configurar, credenciales, red, tiempo agotado) se registra y se convierte en ServiceUnavailableException.
internal sealed class SmtpEnviadorCorreo : IEnviadorCorreo
{
    private const string MensajeNoDisponible = "El servicio de correo no está disponible en este momento.";

    private readonly CorreoSettings _opciones;
    private readonly ILogger<SmtpEnviadorCorreo> _logger;

    public SmtpEnviadorCorreo(IOptions<CorreoSettings> opciones, ILogger<SmtpEnviadorCorreo> logger)
    {
        _opciones = opciones.Value;
        _logger = logger;
    }

    public async Task EnviarVerificacionAsync(string correo, string nombre, string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_opciones.Host) || string.IsNullOrWhiteSpace(_opciones.UrlVerificacion))
        {
            _logger.LogError("Correo:Host o Correo:UrlVerificacion no están configurados. Define Correo__Host, " +
                             "Correo__Usuario, Correo__Password y Correo__UrlVerificacion para enviar correos.");
            throw new ServiceUnavailableException(MensajeNoDisponible);
        }

        // El enlace es una credencial (activa la cuenta): nunca se manda por http salvo a un front en localhost
        if (!Tls.EsUrlSegura(_opciones.UrlVerificacion))
        {
            _logger.LogError("Correo:UrlVerificacion debe empezar con https:// (solo localhost puede usar http).");
            throw new ServiceUnavailableException(MensajeNoDisponible);
        }

        var enlace = $"{_opciones.UrlVerificacion}?token={Uri.EscapeDataString(token)}";
        var mensaje = new MimeMessage
        {
            Subject = "Verifica tu correo en CampusBite",
            Body = new BodyBuilder { HtmlBody = Html(nombre, enlace), TextBody = Texto(nombre, enlace) }.ToMessageBody()
        };
        mensaje.From.Add(new MailboxAddress(_opciones.NombreRemitente,
            string.IsNullOrWhiteSpace(_opciones.Remitente) ? _opciones.Usuario : _opciones.Remitente));
        mensaje.To.Add(new MailboxAddress(nombre, correo));

        try
        {
            using var cliente = new SmtpClient { Timeout = _opciones.TiempoLimiteSegundos * 1000 };
            await cliente.ConnectAsync(_opciones.Host, _opciones.Puerto, Tls.SeguridadSmtp(_opciones.Host, _opciones.Puerto), cancellationToken);
            if (!string.IsNullOrEmpty(_opciones.Usuario))
            {
                await cliente.AuthenticateAsync(_opciones.Usuario, _opciones.Password, cancellationToken);
            }
            await cliente.SendAsync(mensaje, cancellationToken);
            await cliente.DisconnectAsync(true, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "No se pudo enviar el correo de verificación a {Correo}", correo);
            throw new ServiceUnavailableException(MensajeNoDisponible);
        }
    }

    // El nombre viene del usuario: se escapa para que no pueda inyectar HTML en el correo
    private static string Html(string nombre, string enlace) => $"""
        <div style="font-family:Arial,sans-serif;max-width:480px;margin:auto;color:#222">
          <h2 style="color:#c2410c">¡Hola, {WebUtility.HtmlEncode(nombre)}!</h2>
          <p>Gracias por registrarte en <strong>CampusBite</strong>. Para activar tu cuenta, confirma tu correo:</p>
          <p style="text-align:center;margin:32px 0">
            <a href="{WebUtility.HtmlEncode(enlace)}"
               style="background:#c2410c;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none">
              Verificar mi correo
            </a>
          </p>
          <p style="font-size:13px;color:#555">El enlace vence en 24 horas y solo funciona una vez. Si no creaste esta
          cuenta, ignora este mensaje.</p>
        </div>
        """;

    private static string Texto(string nombre, string enlace) =>
        $"Hola, {nombre}.\n\nPara activar tu cuenta de CampusBite abre este enlace (vence en 24 horas):\n{enlace}\n\n" +
        "Si no creaste esta cuenta, ignora este mensaje.";
}
