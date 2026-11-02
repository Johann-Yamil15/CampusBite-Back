using CampusBite.Infrastructure.Seguridad;
using MailKit.Security;
using Npgsql;

namespace CampusBite.Api.Tests.Common;

// JY-Sprint4 31/10/2026: reglas de TLS (todo cifrado salvo hacia localhost)
public class TlsTests
{
    [Theory]
    [InlineData("localhost", true)]
    [InlineData("LOCALHOST", true)]
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("ep-algo.neon.tech", false)]
    [InlineData("127.0.0.2", false)]
    [InlineData(null, false)]
    public void EsLocal_solo_reconoce_la_propia_maquina(string? host, bool esperado)
    {
        Assert.Equal(esperado, Tls.EsLocal(host));
    }

    [Theory]
    [InlineData("Host=ep-algo.neon.tech;Database=neondb;Username=u;Password=p", SslMode.Require)]
    [InlineData("Host=ep-algo.neon.tech;Database=neondb;Username=u;Password=p;SSL Mode=Disable", SslMode.Require)]
    [InlineData("Host=ep-algo.neon.tech;Database=neondb;Username=u;Password=p;SSL Mode=Prefer", SslMode.Require)]
    [InlineData("Host=ep-algo.neon.tech;Database=neondb;Username=u;Password=p;SSL Mode=VerifyFull", SslMode.VerifyFull)]
    [InlineData("Host=localhost;Database=campusbite;Username=u;Password=p;SSL Mode=Disable", SslMode.Disable)]
    public void La_BD_remota_siempre_usa_TLS_y_nunca_se_baja_un_modo_mas_estricto(string cadena, SslMode esperado)
    {
        var resultado = new NpgsqlConnectionStringBuilder(Tls.ExigirSslEnBd(cadena));

        Assert.Equal(esperado, resultado.SslMode);
        Assert.Equal("p", resultado.Password);                            // el resto de la cadena no cambia
    }

    [Theory]
    [InlineData("smtp.gmail.com", 587, SecureSocketOptions.StartTls)]
    [InlineData("smtp.gmail.com", 25, SecureSocketOptions.StartTls)]
    [InlineData("smtp.gmail.com", 465, SecureSocketOptions.SslOnConnect)]
    [InlineData("127.0.0.1", 2525, SecureSocketOptions.StartTlsWhenAvailable)]
    public void El_SMTP_remoto_exige_TLS(string host, int puerto, SecureSocketOptions esperado)
    {
        Assert.Equal(esperado, Tls.SeguridadSmtp(host, puerto));
    }

    [Theory]
    [InlineData("https://campusbite-front.azurewebsites.net/verificar-correo", true)]
    [InlineData("http://localhost:5173/verificar-correo", true)]
    [InlineData("http://127.0.0.1:5173/verificar-correo", true)]
    [InlineData("http://campusbite-front.azurewebsites.net/verificar-correo", false)]
    [InlineData("ftp://localhost/verificar", false)]
    [InlineData("/verificar-correo", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Los_enlaces_del_correo_van_por_https_salvo_localhost(string? url, bool esperado)
    {
        Assert.Equal(esperado, Tls.EsUrlSegura(url));
    }
}
