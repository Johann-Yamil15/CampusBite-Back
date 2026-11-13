using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using CampusBite.Api.Tests.Common;
using CampusBite.Api.Tests.Seguridad;
using CampusBite.Application.Common.Correo;
using CampusBite.Domain.Common.Exceptions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace CampusBite.Api.Tests.Correo;

// SmtpEnviadorCorreo (MailKit) de punta a punta contra un servidor SMTP local de prueba (ServidorSmtpDePrueba).
// El enviador es internal: se obtiene de la DI real de la API (sin tocar el código de producción) con la sección
// "Correo" configurada para la prueba. Nunca se manda un correo real ni se usan credenciales reales.
// "Aceptado" aquí significa que el servidor SMTP respondió 250 al DATA: no prueba que llegue a una bandeja.
public class SmtpEnviadorCorreoTests : IClassFixture<ApiDePrueba>
{
    private const string Token = "TokEnClaro_de-prueba_123";
    private const string Usuario = "usuario-de-prueba@ejemplo.test";
    private const string Clave = "clave-smtp-de-prueba";
    private readonly ApiDePrueba _api;

    public SmtpEnviadorCorreoTests(ApiDePrueba api)
    {
        _api = api;
    }

    private (IEnviadorCorreo Enviador, LogEnMemoria Log) Crear(int puerto, Dictionary<string, string?>? cambios = null)
    {
        var log = new LogEnMemoria();
        var valores = new Dictionary<string, string?>
        {
            ["Correo:Host"] = "127.0.0.1",
            ["Correo:Puerto"] = puerto.ToString(),
            ["Correo:Usuario"] = Usuario,
            ["Correo:Password"] = Clave,
            ["Correo:Remitente"] = "no-responder@ejemplo.test",
            ["Correo:NombreRemitente"] = "CampusBite Pruebas",
            ["Correo:TiempoLimiteSegundos"] = "2",
            ["Correo:UrlVerificacion"] = "http://localhost:5173/verificar-correo"
        };
        foreach (var (k, v) in cambios ?? [])
        {
            valores[k] = v;
        }
        var fabrica = _api.WithWebHostBuilder(b =>
        {
            foreach (var (k, v) in valores)
            {
                b.UseSetting(k, v);
            }
            b.ConfigureLogging(l => l.AddProvider(log));
        });
        return (fabrica.Services.GetRequiredService<IEnviadorCorreo>(), log);
    }

    private static MimeMessage Leer(string crudo) => MimeMessage.Load(new MemoryStream(Encoding.UTF8.GetBytes(crudo)));

    [Fact]
    public async Task Conecta_se_autentica_y_el_SMTP_acepta_el_correo_con_el_enlace_correcto()
    {
        await using var smtp = new ServidorSmtpDePrueba();
        var (enviador, log) = Crear(smtp.Puerto);

        await enviador.EnviarVerificacionAsync("ana.lopez@uttt.edu.mx", "Ana <b>López</b>", Token);

        var mensaje = Leer(Assert.Single(smtp.Mensajes));
        Assert.Equal("Verifica tu correo en CampusBite", mensaje.Subject);
        var para = Assert.Single(mensaje.To.Mailboxes);
        Assert.Equal(("ana.lopez@uttt.edu.mx", "Ana <b>López</b>"), (para.Address, para.Name));
        var de = Assert.Single(mensaje.From.Mailboxes);
        Assert.Equal(("no-responder@ejemplo.test", "CampusBite Pruebas"), (de.Address, de.Name));
        Assert.Contains($"http://localhost:5173/verificar-correo?token={Token}", mensaje.TextBody);
        Assert.Contains("Hola, Ana <b>López</b>.", mensaje.TextBody);
        Assert.Contains("&lt;b&gt;", mensaje.HtmlBody);                                  // el nombre no inyecta HTML
        Assert.DoesNotContain("<b>López</b>", mensaje.HtmlBody);
        Assert.Contains("<ana.lopez@uttt.edu.mx>", Assert.Single(smtp.Destinatarios));
        Assert.DoesNotContain(log.Registros, r => r.Nivel >= LogLevel.Warning && r.Categoria.EndsWith("SmtpEnviadorCorreo"));
    }

    [Fact]
    public async Task El_token_se_escapa_en_la_URL()
    {
        await using var smtp = new ServidorSmtpDePrueba();
        var (enviador, _) = Crear(smtp.Puerto);

        await enviador.EnviarVerificacionAsync("ana@uttt.edu.mx", "Ana", "a b/c?d=1&e");

        Assert.Contains("verificar-correo?token=a%20b%2Fc%3Fd%3D1%26e", Leer(Assert.Single(smtp.Mensajes)).TextBody);
    }

    [Fact]
    public async Task Sin_remitente_usa_el_usuario_SMTP_como_direccion_de_envio()
    {
        await using var smtp = new ServidorSmtpDePrueba();
        var (enviador, _) = Crear(smtp.Puerto, new() { ["Correo:Remitente"] = "" });

        await enviador.EnviarVerificacionAsync("ana@uttt.edu.mx", "Ana", Token);

        Assert.Equal(Usuario, Assert.Single(Leer(Assert.Single(smtp.Mensajes)).From.Mailboxes).Address);
    }

    [Fact]
    public async Task Sin_usuario_no_intenta_autenticarse_y_el_servidor_local_lo_acepta()
    {
        await using var smtp = new ServidorSmtpDePrueba(ServidorSmtpDePrueba.Modo.RechazaAutenticacion);
        var (enviador, _) = Crear(smtp.Puerto, new() { ["Correo:Usuario"] = "", ["Correo:Password"] = "" });

        // Si intentara AUTH, este servidor respondería 535 y fallaría: que pase prueba que no lo intenta
        await enviador.EnviarVerificacionAsync("ana@uttt.edu.mx", "Ana", Token);

        Assert.Single(smtp.Mensajes);
    }

    [Fact]
    public async Task Credenciales_rechazadas_dan_503_y_se_registra_el_error_con_la_excepcion()
    {
        await using var smtp = new ServidorSmtpDePrueba(ServidorSmtpDePrueba.Modo.RechazaAutenticacion);
        var (enviador, log) = Crear(smtp.Puerto);

        var ex = await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            enviador.EnviarVerificacionAsync("ana@uttt.edu.mx", "Ana", Token));

        Assert.Equal("El servicio de correo no está disponible en este momento.", ex.Message);
        Assert.Empty(smtp.Mensajes);
        var error = Assert.Single(log.Registros, r => r.Nivel == LogLevel.Error && r.Categoria.EndsWith("SmtpEnviadorCorreo"));
        Assert.IsType<MailKit.Security.AuthenticationException>(error.Excepcion);
        Assert.DoesNotContain(Clave, error.Mensaje);
    }

    [Fact]
    public async Task Respuesta_SMTP_negativa_al_destinatario_da_503()
    {
        await using var smtp = new ServidorSmtpDePrueba(ServidorSmtpDePrueba.Modo.RechazaDestinatario);
        var (enviador, log) = Crear(smtp.Puerto);

        await Assert.ThrowsAsync<ServiceUnavailableException>(() => enviador.EnviarVerificacionAsync("ana@uttt.edu.mx", "Ana", Token));

        Assert.Empty(smtp.Mensajes);
        var error = Assert.Single(log.Registros, r => r.Nivel == LogLevel.Error && r.Categoria.EndsWith("SmtpEnviadorCorreo"));
        var comando = Assert.IsType<MailKit.Net.Smtp.SmtpCommandException>(error.Excepcion);
        Assert.Equal(MailKit.Net.Smtp.SmtpStatusCode.MailboxUnavailable, comando.StatusCode);
    }

    [Fact]
    public async Task Servidor_que_no_responde_se_corta_por_tiempo_y_da_503()
    {
        await using var smtp = new ServidorSmtpDePrueba(ServidorSmtpDePrueba.Modo.NoResponde);
        var (enviador, log) = Crear(smtp.Puerto);
        var reloj = Stopwatch.StartNew();

        await Assert.ThrowsAsync<ServiceUnavailableException>(() => enviador.EnviarVerificacionAsync("ana@uttt.edu.mx", "Ana", Token));

        Assert.InRange(reloj.Elapsed, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(15));
        Assert.Contains(log.Registros, r => r.Nivel == LogLevel.Error && r.Excepcion is not null);
    }

    [Fact]
    public async Task Servidor_no_disponible_da_503()
    {
        var libre = new TcpListener(IPAddress.Loopback, 0);
        libre.Start();
        var puerto = ((IPEndPoint)libre.LocalEndpoint).Port;
        libre.Stop();
        // En Windows rechazar una conexión local tarda ~2 s (reintentos de SYN): se da más tiempo que eso
        var (enviador, log) = Crear(puerto, new() { ["Correo:TiempoLimiteSegundos"] = "15" });

        await Assert.ThrowsAsync<ServiceUnavailableException>(() => enviador.EnviarVerificacionAsync("ana@uttt.edu.mx", "Ana", Token));

        Assert.Contains(log.Registros, r => r.Nivel == LogLevel.Error && r.Excepcion is SocketException);
    }

    [Fact]
    public async Task Si_quien_llama_cancela_sale_la_cancelacion_y_no_un_503()
    {
        await using var smtp = new ServidorSmtpDePrueba(ServidorSmtpDePrueba.Modo.NoResponde);
        var (enviador, log) = Crear(smtp.Puerto, new() { ["Correo:TiempoLimiteSegundos"] = "30" });
        using var cancelar = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            enviador.EnviarVerificacionAsync("ana@uttt.edu.mx", "Ana", Token, cancelar.Token));

        Assert.DoesNotContain(log.Registros, r => r.Nivel == LogLevel.Error && r.Categoria.EndsWith("SmtpEnviadorCorreo"));
    }

    [Theory]
    [InlineData("", "http://localhost:5173/verificar-correo")]                // sin host
    [InlineData("127.0.0.1", "")]                                            // sin URL del frontend
    [InlineData("127.0.0.1", "http://front.ejemplo.com/verificar-correo")]   // http fuera de localhost
    [InlineData("127.0.0.1", "/verificar-correo")]                           // URL relativa
    public async Task Configuracion_incompleta_o_insegura_da_503_sin_conectar(string host, string url)
    {
        await using var smtp = new ServidorSmtpDePrueba();
        var (enviador, log) = Crear(smtp.Puerto, new() { ["Correo:Host"] = host, ["Correo:UrlVerificacion"] = url });

        await Assert.ThrowsAsync<ServiceUnavailableException>(() => enviador.EnviarVerificacionAsync("ana@uttt.edu.mx", "Ana", Token));

        Assert.Equal(0, smtp.Conexiones);
        Assert.Contains(log.Registros, r => r.Nivel == LogLevel.Error && r.Mensaje.Contains("Correo:"));
    }

    [Fact]
    public async Task La_conexion_se_cierra_al_terminar_cada_envio()
    {
        await using var smtp = new ServidorSmtpDePrueba();
        var (enviador, _) = Crear(smtp.Puerto);

        await enviador.EnviarVerificacionAsync("uno@uttt.edu.mx", "Uno", Token);
        await enviador.EnviarVerificacionAsync("dos@uttt.edu.mx", "Dos", Token);

        Assert.Equal(2, smtp.Conexiones);                      // una conexión por envío, cerrada con QUIT
        Assert.Equal(2, smtp.Mensajes.Count);
    }

    // ---- Defecto D-1 (documentado, sin corregir: requiere autorización) -------------------------------------------
    // El mensaje (new MailboxAddress) se arma ANTES del try: una dirección que [EmailAddress] y la BD aceptan pero
    // MimeKit no ("a..b@x.com", "a,b@x.com", "(c)a@x.com") lanza ParseException, que no se convierte en 503; el
    // registro de alumno responde 500 con la cuenta ya creada. Estas pruebas expresan el contrato correcto y quedan
    // omitidas hasta que se apruebe la corrección (no se legitima el comportamiento actual).
    private const string MotivoD1 = "Defecto D-1: ParseException fuera del try en SmtpEnviadorCorreo (pendiente de autorización)";

    [Theory(Skip = MotivoD1)]
    [InlineData("a..b@x.com")]
    [InlineData("a,b@x.com")]
    public async Task Destinatario_que_MimeKit_rechaza_deberia_dar_503_sin_conectar(string correo)
    {
        await using var smtp = new ServidorSmtpDePrueba();
        var (enviador, _) = Crear(smtp.Puerto);

        await Assert.ThrowsAsync<ServiceUnavailableException>(() => enviador.EnviarVerificacionAsync(correo, "Ana", Token));
        Assert.Equal(0, smtp.Conexiones);
    }

    [Fact(Skip = MotivoD1)]
    public async Task Remitente_invalido_deberia_dar_503_sin_conectar()
    {
        await using var smtp = new ServidorSmtpDePrueba();
        var (enviador, _) = Crear(smtp.Puerto, new() { ["Correo:Remitente"] = "remitente-invalido" });

        await Assert.ThrowsAsync<ServiceUnavailableException>(() => enviador.EnviarVerificacionAsync("ana@uttt.edu.mx", "Ana", Token));
        Assert.Equal(0, smtp.Conexiones);
    }
}
