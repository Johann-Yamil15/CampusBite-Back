using MailKit.Security;
using Npgsql;

namespace CampusBite.Infrastructure.Seguridad;

// JY-Sprint4 31/10/2026: todo lo que sale a internet va cifrado (TLS). Solo se permite sin cifrar dentro de la red
// local de desarrollo: la propia máquina, IPs privadas (Docker/WSL) y nombres sin punto (servicios de docker-compose).
public static class Tls
{
    public static bool EsLocal(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }
        if (System.Net.IPAddress.TryParse(host, out var ip))
        {
            if (System.Net.IPAddress.IsLoopback(ip))
            {
                return true;
            }
            var b = ip.GetAddressBytes();
            return b.Length == 4 && (b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168));
        }
        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || !host.Contains('.');
    }

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
