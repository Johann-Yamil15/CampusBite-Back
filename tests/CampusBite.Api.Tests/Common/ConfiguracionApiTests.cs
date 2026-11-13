using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using CampusBite.Api.Common.Extensions;
using CampusBite.Api.Common.Settings;
using CampusBite.Api.Tests.Seguridad;
using CampusBite.Infrastructure;
using CampusBite.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CampusBite.Api.Tests.Common;

// Configuración de arranque de la API (ServiceCollectionExtensions e Infrastructure.DependencyInjection) con
// configuraciones válidas e inválidas, más el límite de intentos (429) de punta a punta.
public class ConfiguracionApiTests : IClassFixture<ApiDePrueba>
{
    private const string LlaveValida = "llave_de_pruebas_de_configuracion_de_32_o_mas";
    private readonly ApiDePrueba _api;

    public ConfiguracionApiTests(ApiDePrueba api)
    {
        _api = api;
    }

    private static IConfiguration Config(params (string Clave, string? Valor)[] valores) =>
        new ConfigurationBuilder().AddInMemoryCollection(valores.Select(v => new KeyValuePair<string, string?>(v.Clave, v.Valor))).Build();

    // ---- JWT ----------------------------------------------------------------------------------------------------

    public static TheoryData<string?, string?, string?, string?, string> JwtInvalidos => new()
    {
        { null, "emisor", "audiencia", "60", "Jwt:Key" },                                  // sin llave
        { "corta", "emisor", "audiencia", "60", "Jwt:Key" },                               // llave < 32 bytes
        { new string('x', 31), "emisor", "audiencia", "60", "Jwt:Key" },                  // un byte menos del mínimo
        { LlaveValida, "", "audiencia", "60", "Jwt:Issuer" },
        { LlaveValida, "emisor", " ", "60", "Jwt:Issuer" },                               // el mensaje nombra los 3
        { LlaveValida, "emisor", "audiencia", "0", "Jwt:Issuer" }
    };

    [Theory]
    [MemberData(nameof(JwtInvalidos))]
    public void JWT_mal_configurado_impide_arrancar(string? llave, string? emisor, string? audiencia, string? minutos, string menciona)
    {
        var config = Config(("Jwt:Key", llave), ("Jwt:Issuer", emisor), ("Jwt:Audience", audiencia), ("Jwt:ExpirationMinutes", minutos));

        var ex = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddJwtAuthentication(config));

        Assert.Contains(menciona, ex.Message);
    }

    [Fact]
    public void JWT_valido_configura_la_validacion_estricta()
    {
        var servicios = new ServiceCollection().AddLogging();
        servicios.AddJwtAuthentication(Config(("Jwt:Key", LlaveValida), ("Jwt:Issuer", "emisor"),
            ("Jwt:Audience", "audiencia"), ("Jwt:ExpirationMinutes", "30")));

        var opciones = servicios.BuildServiceProvider()
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);
        var p = opciones.TokenValidationParameters;

        Assert.False(opciones.MapInboundClaims);
        Assert.Equal(("emisor", "audiencia"), (p.ValidIssuer, p.ValidAudience));
        Assert.True(p.ValidateIssuer && p.ValidateAudience && p.ValidateIssuerSigningKey && p.ValidateLifetime);
        Assert.Equal([SecurityAlgorithms.HmacSha256], p.ValidAlgorithms);
        Assert.Equal(TimeSpan.FromMinutes(1), p.ClockSkew);
        Assert.Equal(Encoding.UTF8.GetBytes(LlaveValida), ((SymmetricSecurityKey)p.IssuerSigningKey).Key);
        Assert.Equal(("sub", "role"), (p.NameClaimType, p.RoleClaimType));
    }

    // ---- CORS ---------------------------------------------------------------------------------------------------

    private static CorsPolicy Cors(string? origenes)
    {
        var servicios = new ServiceCollection();
        servicios.AddCorsPolicy(Config(("Cors:AllowedOrigins", origenes)));
        return servicios.BuildServiceProvider().GetRequiredService<IOptions<CorsOptions>>().Value.GetPolicy(CorsSettings.PolicyName)!;
    }

    [Fact]
    public void CORS_con_origenes_solo_permite_esos_sin_barra_final_ni_repetidos_y_con_credenciales()
    {
        var politica = Cors(" https://front.ejemplo.test/ , https://otro.ejemplo.test,https://FRONT.ejemplo.test ,, ");

        Assert.Equal(["https://front.ejemplo.test", "https://otro.ejemplo.test"], politica.Origins);
        Assert.False(politica.AllowAnyOrigin);
        Assert.True(politica.SupportsCredentials);
        Assert.True(politica.AllowAnyHeader && politica.AllowAnyMethod);
    }

    // Comportamiento ACTUAL documentado (hallazgo S5122 de SonarCloud): sin orígenes configurados se permite cualquiera,
    // pero nunca con credenciales. La propuesta de corrección (fallar fuera de Development) va aparte.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" , ")]
    public void CORS_sin_origenes_permite_cualquiera_pero_sin_credenciales(string? origenes)
    {
        var politica = Cors(origenes);

        Assert.True(politica.AllowAnyOrigin);
        Assert.False(politica.SupportsCredentials);
    }

    // ---- Límite de intentos (429) ---------------------------------------------------------------------------------

    [Fact]
    public async Task Al_pasar_el_limite_de_intentos_responde_429_con_Retry_After_y_ProblemDetails()
    {
        var cliente = _api.WithWebHostBuilder(b =>
        {
            b.UseSetting("RateLimit:AuthPermitLimit", "2");
            b.UseSetting("RateLimit:AuthWindowSeconds", "60");
        }).CreateClient();
        var cuerpo = new { correo = "no-es-correo", password = "" };       // 400 por validación, pero consume intento

        var primero = await cliente.PostAsJsonAsync("/api/auth/login", cuerpo);
        var segundo = await cliente.PostAsJsonAsync("/api/auth/login", cuerpo);
        var tercero = await cliente.PostAsJsonAsync("/api/auth/login", cuerpo);

        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.BadRequest), (primero.StatusCode, segundo.StatusCode));
        Assert.Equal(HttpStatusCode.TooManyRequests, tercero.StatusCode);
        Assert.Equal("application/problem+json", tercero.Content.Headers.ContentType?.MediaType);
        var espera = int.Parse(Assert.Single(tercero.Headers.GetValues("Retry-After")));
        Assert.InRange(espera, 1, 60);
        var problema = (await tercero.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal(429, (int)problema["status"]!);
        Assert.Equal("Demasiados intentos. Espera un momento y vuelve a intentarlo.", (string?)problema["detail"]);
        Assert.Equal("/api/auth/login", (string?)problema["instance"]);
    }

    [Fact]
    public async Task El_limite_solo_aplica_a_las_rutas_de_autenticacion()
    {
        var cliente = _api.WithWebHostBuilder(b => b.UseSetting("RateLimit:AuthPermitLimit", "1")).CreateClient();

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/pruebas/publica")).StatusCode);
        }
    }

    // ---- Base de datos (Infrastructure.DependencyInjection + DatabaseInfo) ---------------------------------------

    private static IServiceProvider Infraestructura(params (string, string?)[] valores)
    {
        var servicios = new ServiceCollection().AddLogging();
        servicios.AddInfrastructure(Config(valores));
        return servicios.BuildServiceProvider();
    }

    [Fact]
    public void Sin_cadena_de_conexion_la_API_no_arranca_y_dice_cual_falta()
    {
        var porDefecto = Assert.Throws<InvalidOperationException>(() => Infraestructura());
        var elegida = Assert.Throws<InvalidOperationException>(() => Infraestructura(("Database:Conexion", " Local ")));

        Assert.Contains("ConnectionStrings:Database", porDefecto.Message);
        Assert.Contains("ConnectionStrings:Local", elegida.Message);
    }

    [Fact]
    public void Una_conexion_local_elegida_por_nombre_no_es_produccion_ni_fuerza_TLS()
    {
        var sp = Infraestructura(("Database:Conexion", "Local"),
            ("ConnectionStrings:Local", "Host=localhost;Database=campusbite;Username=app_backend;Password=x"));

        var info = sp.GetRequiredService<DatabaseInfo>();
        Assert.Equal(new DatabaseInfo("Local", "localhost", "campusbite", "app_backend"), info);
        Assert.False(info.EsProduccion);
        using var scope = sp.CreateScope();
        Assert.DoesNotContain("SSL Mode", scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetConnectionString());
    }

    [Fact]
    public void Una_conexion_remota_es_produccion_y_exige_TLS()
    {
        var sp = Infraestructura(("ConnectionStrings:Database",
            "Host=bd.remota.ejemplo.test;Database=campusbite;Username=app;Password=x;SSL Mode=Prefer"));

        var info = sp.GetRequiredService<DatabaseInfo>();
        Assert.Equal(("Database", "bd.remota.ejemplo.test"), (info.Conexion, info.Host));
        Assert.True(info.EsProduccion);
        using var scope = sp.CreateScope();
        Assert.Contains("SSL Mode=Require", scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetConnectionString());
    }

    [Theory]
    [InlineData("Local", "localhost", false)]
    [InlineData("Local", "127.0.0.1", false)]
    [InlineData("Local", "::1", false)]
    [InlineData("Local", "db.ejemplo.test", true)]          // un servidor que no es la máquina local cuenta como producción
    [InlineData("Produccion", "localhost", true)]           // elegida como producción aunque sea local
    [InlineData("PRODUCCION", "127.0.0.1", true)]
    public void DatabaseInfo_decide_si_es_produccion(string conexion, string host, bool esperado)
    {
        Assert.Equal(esperado, new DatabaseInfo(conexion, host, "campusbite", "app").EsProduccion);
    }
}
