using MailKit.Security;
using Npgsql;

namespace CampusBite.Infrastructure.Seguridad;

// JY-Sprint4 31/10/2026: todo lo que sale de la máquina va cifrado (TLS). Solo se permite sin cifrar hacia la propia
// máquina (localhost), que es donde corren la BD y el SMTP de prueba en desarrollo.
public static class Tls
{
    public static bool EsLocal(string? host) =>
        host is not null && (host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host == "127.0.0.1" || host == "::1");

    // BD remota (Neon): exige TLS aunque la cadena no lo diga o pida menos (Disable/Allow/Prefer)
    public static string ExigirSslEnBd(string connectionString)
    {
        var datos = new NpgsqlConnectionStringBuilder(connectionString);
        if (!EsLocal(datos.Host) && datos.SslMode is SslMode.Disable or SslMode.Allow or SslMode.Prefer)
        {
            datos.SslMode = SslMode.Require;
        }
        return datos.ConnectionString;
    }

    // SMTP remoto: TLS obligatorio (465 = TLS directo; los demás puertos, STARTTLS sin opción a seguir en claro)
    public static SecureSocketOptions SeguridadSmtp(string host, int puerto) =>
        EsLocal(host) ? SecureSocketOptions.StartTlsWhenAvailable
        : puerto == 465 ? SecureSocketOptions.SslOnConnect
        : SecureSocketOptions.StartTls;

    // Enlaces que se mandan por correo: https, salvo una página del front corriendo en localhost
    public static bool EsUrlSegura(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && EsLocal(uri.Host)));
}
