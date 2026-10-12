using System.Net.Http.Headers;
using System.Text;
using CampusBite.Api.Common.Security;
using CampusBite.Application.Common.Security;
using CampusBite.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CampusBite.Api.Tests.Seguridad;

// JY-Sprint2 10/10/2026: levanta la API real en memoria (mismo Program.cs) con configuración de prueba.
// No toca la base de datos: la cadena de conexión es falsa y estas pruebas no llaman repositorios.
public class ApiDePrueba : WebApplicationFactory<Program>
{
    public const string Llave = "llave_de_pruebas_de_integracion_de_32_o_mas_caracteres";
    public const string Emisor = "CampusBite.API";
    public const string Audiencia = "CampusBite.PWA";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Jwt:Key", Llave);
        builder.UseSetting("Jwt:Issuer", Emisor);
        builder.UseSetting("Jwt:Audience", Audiencia);
        builder.UseSetting("Jwt:ExpirationMinutes", "60");
        builder.UseSetting("Database:Conexion", "");
        builder.UseSetting("ConnectionStrings:Database", "Host=localhost;Database=sin_uso;Username=nadie;Password=nada");

        // Agrega los endpoints de prueba (RutasDePruebaController) sin tocar la API de verdad
        builder.ConfigureTestServices(services =>
            services.AddControllers().AddApplicationPart(typeof(RutasDePruebaController).Assembly));
    }

    // Token emitido por el TokenService real de la API
    public HttpClient ClienteCon(RolUsuario rol, Guid? idUsuario = null)
    {
        using var scope = Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var (token, _) = tokens.GenerarToken(idUsuario ?? Guid.NewGuid(), "prueba@ejemplo.test", rol);
        return ClienteConToken(token);
    }

    public HttpClient ClienteConToken(string token)
    {
        var cliente = CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return cliente;
    }

    // Token hecho a mano para los casos que TokenService no genera (vencido, otra llave, rol desconocido)
    public static string TokenManual(string rol, DateTime expira, string llave = Llave) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Emisor,
            Audience = Audiencia,
            NotBefore = expira.AddHours(-2),
            IssuedAt = expira.AddHours(-2),
            Expires = expira,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = Guid.NewGuid().ToString(),
                ["role"] = rol
            },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(llave)), SecurityAlgorithms.HmacSha256)
        });
}

// Endpoints que solo existen en las pruebas: uno por cada caso de protección
[ApiController]
[Route("pruebas")]
public class RutasDePruebaController : ControllerBase
{
    [HttpGet("publica")]
    [AllowAnonymous]
    public IActionResult Publica() => Ok();

    [HttpGet("sin-atributo")]           // no dice nada: debe quedar protegida por la FallbackPolicy
    public IActionResult SinAtributo() => Ok();

    [HttpGet("alumno")]
    [Authorize(Policy = PoliticasAutorizacion.SoloAlumno)]
    public IActionResult Alumno() => Ok();

    [HttpGet("personal")]
    [Authorize(Policy = PoliticasAutorizacion.PersonalCafeteria)]
    public IActionResult Personal() => Ok();

    [HttpGet("admin")]
    [Authorize(Policy = PoliticasAutorizacion.SoloAdminSistema)]
    public IActionResult Admin() => Ok();

    // Devuelve el id de la sesión aunque la query traiga otro: el actor sale del token, no de la petición
    [HttpGet("quien-soy")]
    public IActionResult QuienSoy([FromQuery] Guid? idUsuario, [FromServices] IUsuarioActual usuario) =>
        Ok(new { id = usuario.Id, idDeLaQuery = idUsuario });
}
