using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json.Nodes;
using CampusBite.Api.Common.Extensions;
using CampusBite.Api.Common.Filters;
using CampusBite.Api.Common.Security;
using CampusBite.Api.Common.Settings;
using CampusBite.Api.Tests.Seguridad;
using CampusBite.Application.Productos.Dtos;
using CampusBite.Domain.Common.Exceptions;
using CampusBite.Infrastructure;
using CampusBite.Infrastructure.Persistence;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CampusBite.Api.Tests.Common;

// Ramas de configuración y de seguridad que las demás pruebas no recorren: secciones de configuración AUSENTES (no
// vacías), partición del límite de intentos por IP de origen, sesión sin contexto HTTP y respuesta de tiempo agotado.
public class RamasDeConfiguracionTests : IClassFixture<ApiDePrueba>
{
    private readonly ApiDePrueba _api;

    public RamasDeConfiguracionTests(ApiDePrueba api)
    {
        _api = api;
    }

    private static IConfiguration ConfigVacia() => new ConfigurationBuilder().Build();

    // ---- Secciones de configuración ausentes -------------------------------------------------------------------

    [Fact]
    public void Sin_seccion_Jwt_la_API_no_arranca_y_pide_la_llave()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddJwtAuthentication(ConfigVacia()));

        Assert.Contains("Jwt:Key", ex.Message);
    }

    [Fact]
    public void Sin_seccion_Cors_no_se_autoriza_a_ningun_front()
    {
        var servicios = new ServiceCollection();
        servicios.AddCorsPolicy(ConfigVacia());

        var politica = servicios.BuildServiceProvider().GetRequiredService<IOptions<CorsOptions>>().Value
            .GetPolicy(CorsSettings.PolicyName)!;

        Assert.Empty(politica.Origins);
        Assert.False(politica.AllowAnyOrigin);
    }

    [Fact]
    public void Una_cadena_de_conexion_sin_host_base_ni_usuario_se_muestra_con_signos_de_interrogacion()
    {
        var servicios = new ServiceCollection().AddLogging();
        servicios.AddInfrastructure(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:Database"] = "Port=5432;Password=x;Timeout=5" }).Build());

        var info = servicios.BuildServiceProvider().GetRequiredService<DatabaseInfo>();

        Assert.Equal(new DatabaseInfo("Database", "?", "?", "?"), info);
    }

    // ---- Límite de intentos: una partición por IP de origen --------------------------------------------------------

    private HttpClient ClienteConLimiteDeUno() =>
        _api.WithWebHostBuilder(b =>
        {
            b.UseSetting("RateLimit:AuthPermitLimit", "1");
            b.UseSetting("RateLimit:AuthWindowSeconds", "60");
        }).CreateClient();

    private static Task<HttpResponseMessage> Login(HttpClient cliente, string ip)
    {
        var peticion = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { correo = "no-es-correo", password = "" })     // 400, pero consume intento
        };
        peticion.Headers.Add("X-Forwarded-For", ip);
        return cliente.SendAsync(peticion);
    }

    [Fact]
    public async Task El_limite_de_intentos_cuenta_por_IP_de_origen_y_no_mezcla_clientes()
    {
        var cliente = ClienteConLimiteDeUno();

        var primeraDeA = await Login(cliente, "203.0.113.10");
        var segundaDeA = await Login(cliente, "203.0.113.10");
        var primeraDeB = await Login(cliente, "203.0.113.20");

        Assert.Equal(HttpStatusCode.BadRequest, primeraDeA.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, segundaDeA.StatusCode);       // la misma IP agotó su intento
        Assert.Equal(HttpStatusCode.BadRequest, primeraDeB.StatusCode);            // otra IP tiene el suyo intacto
    }

    // ---- Sesión: sin contexto HTTP o sin identidad no hay usuario ------------------------------------------------

    [Fact]
    public void Sin_contexto_HTTP_la_sesion_no_es_valida()
    {
        var usuario = new UsuarioActual(new HttpContextAccessor { HttpContext = null });

        Assert.Throws<UnauthorizedException>(() => usuario.Id);
        Assert.Throws<UnauthorizedException>(() => usuario.Rol);
    }

    [Fact]
    public void Un_principal_sin_identidad_no_es_una_sesion_valida()
    {
        var contexto = new DefaultHttpContext { User = new ClaimsPrincipal() };       // sin ninguna ClaimsIdentity
        var usuario = new UsuarioActual(new HttpContextAccessor { HttpContext = contexto });

        Assert.Null(contexto.User.Identity);
        Assert.Throws<UnauthorizedException>(() => usuario.Id);
    }

    // ---- Subida de archivos: respuesta cuando se agota el tiempo --------------------------------------------------

    [Fact]
    public async Task La_subida_que_tarda_demasiado_responde_504_con_ProblemDetails_en_espanol()
    {
        var servicios = new ServiceCollection().AddLogging().AddOptions();
        servicios.AddSubidaDeArchivos();
        var proveedor = servicios.BuildServiceProvider();
        var politica = proveedor.GetRequiredService<IOptions<RequestTimeoutOptions>>().Value
            .Policies[SubidaDeArchivoAttribute.PoliticaTiempo];
        var contexto = new DefaultHttpContext { RequestServices = proveedor };
        contexto.Request.Path = "/api/cafeterias/x/productos";
        contexto.Response.Body = new MemoryStream();

        await politica.WriteTimeoutResponse!(contexto);

        Assert.Equal(StatusCodes.Status504GatewayTimeout, politica.TimeoutStatusCode);
        Assert.Equal(StatusCodes.Status504GatewayTimeout, contexto.Response.StatusCode);
        contexto.Response.Body.Position = 0;
        var problema = (await JsonNode.ParseAsync(contexto.Response.Body))!;
        Assert.Equal("La subida tardó demasiado. Revisa tu conexión e intenta de nuevo.", (string?)problema["detail"]);
        Assert.Equal("GatewayTimeout", (string?)problema["title"]);
    }

    // ---- Constantes y utilidades ----------------------------------------------------------------------------------

    [Fact]
    public void El_precio_maximo_de_un_producto_cabe_en_NUMERIC_8_2()
    {
        var campo = typeof(GuardarProductoDto).GetField(nameof(GuardarProductoDto.PrecioMaximo))!;

        Assert.Equal(999_999.99m, (decimal)campo.GetValue(null)!);
    }

    [Fact]
    public void Una_fecha_nula_se_manda_a_la_BD_como_NULL_y_una_fecha_con_valor_como_date()
    {
        var sinFecha = Procedimientos.Fecha("fecha", null);
        var conFecha = Procedimientos.Fecha("fecha", new DateOnly(2026, 11, 15));

        Assert.Equal(DBNull.Value, sinFecha.Value);
        Assert.Equal(new DateOnly(2026, 11, 15), conFecha.Value);
        Assert.Equal(NpgsqlTypes.NpgsqlDbType.Date, conFecha.NpgsqlDbType);
    }
}
