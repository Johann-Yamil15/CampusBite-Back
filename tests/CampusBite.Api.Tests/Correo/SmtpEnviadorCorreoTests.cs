using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using CampusBite.Api.Tests.Common;
using CampusBite.Api.Tests.Seguridad;
using CampusBite.Application.Common.Correo;
using CampusBite.Domain.Ordenes;
using CampusBite.Domain.Cafeterias;
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

    // ---- Recuperación de contraseña (AUD-001) -----------------------------------------------------------------------
    private const string UrlRecuperacion = "http://localhost:5173/restablecer-password";

    [Fact]
    public async Task Recuperacion_manda_el_enlace_de_restablecer_con_el_token_escapado_y_el_nombre_sin_HTML()
    {
        await using var smtp = new ServidorSmtpDePrueba();
        var (enviador, log) = Crear(smtp.Puerto, new() { ["Correo:UrlRecuperacion"] = UrlRecuperacion });

        await enviador.EnviarRecuperacionAsync("ana@uttt.edu.mx", "Ana <b>López</b>", "a b/c");

        var mensaje = Leer(Assert.Single(smtp.Mensajes));
        Assert.Equal("Restablece tu contraseña de CampusBite", mensaje.Subject);
        Assert.Contains($"{UrlRecuperacion}?token=a%20b%2Fc", mensaje.TextBody);
        Assert.Contains("&lt;b&gt;", mensaje.HtmlBody);
        Assert.DoesNotContain("<b>López</b>", mensaje.HtmlBody);
        Assert.DoesNotContain(log.Registros, r => r.Nivel >= LogLevel.Warning && r.Categoria.EndsWith("SmtpEnviadorCorreo"));
    }

    [Theory]
    [InlineData("")]                                                     // sin configurar
    [InlineData("http://campusbite-front.azurewebsites.net/restablecer")]  // http fuera de localhost
    public async Task Recuperacion_con_URL_ausente_o_insegura_da_503_sin_conectar(string url)
    {
        await using var smtp = new ServidorSmtpDePrueba();
        var (enviador, log) = Crear(smtp.Puerto, new() { ["Correo:UrlRecuperacion"] = url });

        await Assert.ThrowsAsync<ServiceUnavailableException>(() => enviador.EnviarRecuperacionAsync("ana@uttt.edu.mx", "Ana", Token));

        Assert.Equal(0, smtp.Conexiones);
        Assert.Contains(log.Registros, r => r.Nivel == LogLevel.Error && r.Categoria.EndsWith("SmtpEnviadorCorreo"));
    }

    [Fact]
    public async Task Recuperacion_con_fallo_de_SMTP_da_503_y_el_log_no_lleva_correo_token_ni_excepcion()
    {
        await using var smtp = new ServidorSmtpDePrueba(ServidorSmtpDePrueba.Modo.RechazaAutenticacion);
        var (enviador, log) = Crear(smtp.Puerto, new() { ["Correo:UrlRecuperacion"] = UrlRecuperacion });

        await Assert.ThrowsAsync<ServiceUnavailableException>(() => enviador.EnviarRecuperacionAsync("ana@uttt.edu.mx", "Ana", Token));

        var error = Assert.Single(log.Registros, r => r.Nivel == LogLevel.Error && r.Categoria.EndsWith("SmtpEnviadorCorreo"));
        Assert.Null(error.Excepcion);
        Assert.DoesNotContain("ana@uttt.edu.mx", error.Mensaje);
        Assert.DoesNotContain(Token, error.Mensaje);
        Assert.Contains("AuthenticationException", error.Mensaje);
    }

    // ---- Seguimiento del pedido (JY-Sprint5 27/11/2026) --------------------------------------------------------------
    private static AvisoPedido Pedido(EventoPedido evento, string cafeteria = "Cafetería Central", string? codigo = null,
        string? motivo = null, EstadoPago pago = EstadoPago.Pendiente, MetodoPago metodo = MetodoPago.Transferencia) => new(
        evento, Guid.Parse("00000000-0000-0000-0000-0000000000f1"), cafeteria, "Barra principal",
        new DateTimeOffset(2026, 11, 28, 19, 0, 0, TimeSpan.Zero), 110m, metodo, pago,
        [new LineaAvisoPedido("Chilaquiles verdes", 2, 55m)], codigo, motivo);

    private async Task<MimeMessage> EnviarPedidoAsync(AvisoPedido aviso, Dictionary<string, string?>? cambios = null)
    {
        await using var smtp = new ServidorSmtpDePrueba();
        var (enviador, _) = Crear(smtp.Puerto, cambios);
        await enviador.EnviarEstadoPedidoAsync("ana@uttt.edu.mx", "Ana López", aviso);
        return Leer(Assert.Single(smtp.Mensajes));
    }

    [Theory]
    [InlineData(EventoPedido.PagoConfirmado, "Confirmamos tu pago", "confirmó tu pago")]
    [InlineData(EventoPedido.EnPreparacion, "Tu pedido se está preparando", "ya está preparando tu pedido")]
    [InlineData(EventoPedido.Entregada, "Entregamos tu pedido", "¡Buen provecho!")]
    public async Task Cada_aviso_del_pedido_sale_con_su_asunto_y_su_texto(EventoPedido evento, string asunto, string texto)
    {
        var mensaje = await EnviarPedidoAsync(Pedido(evento));

        Assert.Equal(asunto, mensaje.Subject);
        Assert.Contains(texto, mensaje.TextBody);
        Assert.Contains("Hola, Ana López.", mensaje.TextBody);
        var para = Assert.Single(mensaje.To.Mailboxes);
        Assert.Equal("ana@uttt.edu.mx", para.Address);
    }

    [Fact]
    public async Task El_pedido_recien_creado_con_pago_pendiente_avisa_que_espera_la_confirmacion_del_pago()
    {
        var mensaje = await EnviarPedidoAsync(Pedido(EventoPedido.Creada));

        Assert.Equal("Recibimos tu pedido en Cafetería Central", mensaje.Subject);
        Assert.Contains("está esperando confirmar tu pago por transferencia", mensaje.TextBody);
    }

    [Fact]
    public async Task El_pedido_recien_creado_sin_pago_pendiente_avisa_que_quedo_confirmado()
    {
        var mensaje = await EnviarPedidoAsync(Pedido(EventoPedido.Creada, pago: EstadoPago.Pagado));

        Assert.Contains("Tu pedido quedó confirmado en Cafetería Central", mensaje.TextBody);
    }

    [Theory]
    [InlineData(MetodoPago.Efectivo, "por efectivo")]
    [InlineData(MetodoPago.Tarjeta, "por tarjeta")]
    [InlineData(MetodoPago.Transferencia, "por transferencia")]
    public async Task El_aviso_de_pago_pendiente_nombra_el_metodo_de_pago_elegido(MetodoPago metodo, string esperado)
    {
        var mensaje = await EnviarPedidoAsync(Pedido(EventoPedido.Creada, metodo: metodo));

        Assert.Contains(esperado, mensaje.TextBody);
    }

    [Fact]
    public async Task Un_metodo_de_pago_desconocido_no_rompe_el_aviso()
    {
        var mensaje = await EnviarPedidoAsync(Pedido(EventoPedido.Creada, metodo: (MetodoPago)99));

        Assert.Contains("por el método elegido", mensaje.TextBody);
    }

    [Fact]
    public async Task El_aviso_de_pedido_listo_lleva_el_punto_de_entrega_y_el_codigo_de_recogida()
    {
        var mensaje = await EnviarPedidoAsync(Pedido(EventoPedido.Lista, codigo: "123456"));

        Assert.Equal("¡Tu pedido está listo!", mensaje.Subject);
        Assert.Contains("Pasa a recogerlo en Barra principal", mensaje.TextBody);
        Assert.Contains("Tu código de recogida: 123456", mensaje.TextBody);
        Assert.Contains("123456", mensaje.HtmlBody);
    }

    [Fact]
    public async Task Sin_codigo_en_el_aviso_el_correo_no_lo_menciona()
    {
        var mensaje = await EnviarPedidoAsync(Pedido(EventoPedido.EnPreparacion));

        Assert.DoesNotContain("código de recogida", mensaje.TextBody);
        Assert.DoesNotContain("código de recogida", mensaje.HtmlBody);
    }

    [Fact]
    public async Task El_aviso_de_cancelacion_lleva_el_motivo_cuando_existe_y_funciona_sin_el()
    {
        var conMotivo = await EnviarPedidoAsync(Pedido(EventoPedido.Cancelada, motivo: "Se acabó el producto"));
        var sinMotivo = await EnviarPedidoAsync(Pedido(EventoPedido.Cancelada, motivo: " "));

        Assert.Equal("Tu pedido fue cancelado", conMotivo.Subject);
        Assert.Contains("Motivo: Se acabó el producto", conMotivo.TextBody);
        Assert.DoesNotContain("Motivo:", sinMotivo.TextBody);
        Assert.Contains("fue cancelado.", sinMotivo.TextBody);
    }

    [Fact]
    public async Task El_resumen_muestra_las_lineas_el_total_y_la_hora_de_recogida_del_centro_de_Mexico()
    {
        var mensaje = await EnviarPedidoAsync(Pedido(EventoPedido.EnPreparacion));

        Assert.Contains("2 x Chilaquiles verdes - $110.00", mensaje.TextBody);
        Assert.Contains("Total: $110.00", mensaje.TextBody);
        Assert.Contains("Recogida: 28/11/2026 13:00 en Barra principal", mensaje.TextBody);      // 19:00 UTC = 13:00 en UTC-6
        Assert.Contains("28/11/2026 13:00", mensaje.HtmlBody);
    }

    [Fact]
    public async Task Los_textos_que_vienen_de_la_base_se_escapan_en_el_HTML_y_el_asunto_no_lleva_saltos_de_linea()
    {
        var aviso = Pedido(EventoPedido.Creada, cafeteria: "Café <b>X</b>\r\nBcc: otro@x.com");

        var mensaje = await EnviarPedidoAsync(aviso);

        Assert.DoesNotContain("<b>X</b>", mensaje.HtmlBody);
        Assert.Contains("&lt;b&gt;X&lt;/b&gt;", mensaje.HtmlBody);
        Assert.DoesNotContain('\n', mensaje.Subject!);
        Assert.DoesNotContain('\r', mensaje.Subject!);
        Assert.Null(mensaje.Headers["Bcc"]);                                    // el salto de línea no abre otro encabezado
    }

    [Fact]
    public async Task Con_UrlPedidos_el_correo_trae_el_enlace_para_seguir_el_pedido()
    {
        var mensaje = await EnviarPedidoAsync(Pedido(EventoPedido.EnPreparacion),
            new() { ["Correo:UrlPedidos"] = "http://localhost:4200/pedidos/" });

        Assert.Contains("http://localhost:4200/pedidos/00000000-0000-0000-0000-0000000000f1", mensaje.TextBody);
        Assert.Contains("Seguir mi pedido", mensaje.HtmlBody);
    }

    [Theory]
    [InlineData("")]                                                     // sin configurar
    [InlineData("http://campusbite-front.azurewebsites.net/pedidos")]    // http fuera de localhost: no se manda
    public async Task Sin_una_UrlPedidos_segura_el_correo_sale_sin_enlace_y_no_falla(string url)
    {
        var mensaje = await EnviarPedidoAsync(Pedido(EventoPedido.EnPreparacion), new() { ["Correo:UrlPedidos"] = url });

        Assert.DoesNotContain("Sigue tu pedido", mensaje.TextBody);
        Assert.DoesNotContain("Seguir mi pedido", mensaje.HtmlBody);
    }

    [Fact]
    public async Task Sin_servidor_SMTP_configurado_el_aviso_da_503_y_lo_registra()
    {
        await using var smtp = new ServidorSmtpDePrueba();
        var (enviador, log) = Crear(smtp.Puerto, new() { ["Correo:Host"] = "" });

        await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            enviador.EnviarEstadoPedidoAsync("ana@uttt.edu.mx", "Ana", Pedido(EventoPedido.Lista)));

        Assert.Equal(0, smtp.Conexiones);
        Assert.Contains(log.Registros, r => r.Nivel == LogLevel.Error && r.Categoria.EndsWith("SmtpEnviadorCorreo"));
    }

    [Fact]
    public async Task Si_el_SMTP_rechaza_el_aviso_da_503_y_el_log_no_lleva_el_correo()
    {
        await using var smtp = new ServidorSmtpDePrueba(ServidorSmtpDePrueba.Modo.RechazaAutenticacion);
        var (enviador, log) = Crear(smtp.Puerto);

        await Assert.ThrowsAsync<ServiceUnavailableException>(() =>
            enviador.EnviarEstadoPedidoAsync("ana@uttt.edu.mx", "Ana", Pedido(EventoPedido.Lista, codigo: "123456")));

        var error = Assert.Single(log.Registros, r => r.Nivel == LogLevel.Error && r.Categoria.EndsWith("SmtpEnviadorCorreo"));
        Assert.Null(error.Excepcion);
        Assert.DoesNotContain("ana@uttt.edu.mx", error.Mensaje);
        Assert.DoesNotContain("123456", error.Mensaje);
        Assert.Contains("estado del pedido", error.Mensaje);
    }

    [Fact]
    public async Task Un_evento_desconocido_es_un_defecto_y_no_se_manda_ningun_correo()
    {
        await using var smtp = new ServidorSmtpDePrueba();
        var (enviador, _) = Crear(smtp.Puerto);

        var error = Record.Exception(() => { _ = enviador.EnviarEstadoPedidoAsync("ana@uttt.edu.mx", "Ana", Pedido((EventoPedido)99)); });

        Assert.IsType<ArgumentOutOfRangeException>(error);

        Assert.Equal(0, smtp.Conexiones);
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
