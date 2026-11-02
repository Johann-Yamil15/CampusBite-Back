using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CampusBite.Api.Tests.Seguridad;
using CampusBite.Application.Auth;
using CampusBite.Application.Common.Correo;
using CampusBite.Domain.Auth;
using CampusBite.Domain.Cafeterias;
using CampusBite.Domain.Common.Exceptions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CampusBite.Api.Tests.Auth;

// JY-Sprint4 31/10/2026: verificación de correo de punta a punta con la API real en memoria (AuthService, BCrypt y
// JWT reales). Solo la BD (un repositorio en memoria con las reglas de los sp_*) y el SMTP son falsos.
public class VerificacionCorreoEndpointTests : IClassFixture<ApiDePrueba>
{
    private readonly ApiDePrueba _api;
    private readonly UsuariosEnMemoria _usuarios = new();
    private readonly CorreoFalso _correo = new();

    public VerificacionCorreoEndpointTests(ApiDePrueba api)
    {
        _api = api;
    }

    private HttpClient Cliente() => _api.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
    {
        s.RemoveAll<IUsuarioRepository>();
        s.AddSingleton<IUsuarioRepository>(_usuarios);
        s.RemoveAll<IEnviadorCorreo>();
        s.AddSingleton<IEnviadorCorreo>(_correo);
    })).CreateClient();

    private static object Alumno(string correo = "Ana@UTTT.edu.mx") =>
        new { nombre = "Ana López", correo, password = "Password123" };

    private static object Login(string password = "Password123") => new { correo = "ana@uttt.edu.mx", password };

    [Fact]
    public async Task Registro_de_alumno_responde_201_sin_token_y_manda_el_correo()
    {
        var respuesta = await Cliente().PostAsJsonAsync("/api/auth/registro", Alumno());

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var json = (await respuesta.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("ana@uttt.edu.mx", (string?)json["correo"]);
        Assert.True((bool)json["correoEnviado"]!);
        Assert.Null(json["token"]);
        var enviado = Assert.Single(_correo.Enviados);
        Assert.Equal(("ana@uttt.edu.mx", "Ana López"), (enviado.Correo, enviado.Nombre));
    }

    [Fact]
    public async Task Flujo_completo_registro_login_rechazado_verificacion_y_sesion()
    {
        var cliente = Cliente();
        await cliente.PostAsJsonAsync("/api/auth/registro", Alumno());

        // Sin verificar: 403 con el aviso para el usuario
        var antes = await cliente.PostAsJsonAsync("/api/auth/login", Login());
        Assert.Equal(HttpStatusCode.Forbidden, antes.StatusCode);
        Assert.Contains("Verifica tu correo", await antes.Content.ReadAsStringAsync());

        // Abre el enlace: el frontend manda el token y recibe la sesión
        var verificacion = await cliente.PostAsJsonAsync("/api/auth/verificar-correo", new { token = _correo.Enviados[0].Token });
        Assert.Equal(HttpStatusCode.OK, verificacion.StatusCode);
        var sesion = (await verificacion.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal(nameof(RolUsuario.Alumno), (string?)sesion["rol"]);
        var me = JsonNode.Parse(await _api.ClienteConToken((string)sesion["token"]!).GetStringAsync("/api/auth/me"))!;
        Assert.Equal("ana@uttt.edu.mx", (string?)me["correo"]);

        // Ya puede iniciar sesión y el enlace no se puede volver a usar
        Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsJsonAsync("/api/auth/login", Login())).StatusCode);
        var otraVez = await cliente.PostAsJsonAsync("/api/auth/verificar-correo", new { token = _correo.Enviados[0].Token });
        Assert.Equal(HttpStatusCode.BadRequest, otraVez.StatusCode);
        Assert.Contains("no es válido o ya se usó", await otraVez.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Sin_verificar_y_con_la_contrasena_mal_sigue_siendo_401()
    {
        var cliente = Cliente();
        await cliente.PostAsJsonAsync("/api/auth/registro", Alumno());

        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login", Login("MalaClave1"));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Reenviar_responde_202_igual_exista_o_no_la_cuenta_y_solo_manda_si_existe()
    {
        var cliente = Cliente();
        await cliente.PostAsJsonAsync("/api/auth/registro", Alumno());

        var existe = await cliente.PostAsJsonAsync("/api/auth/reenviar-verificacion", new { correo = "ana@uttt.edu.mx" });
        var noExiste = await cliente.PostAsJsonAsync("/api/auth/reenviar-verificacion", new { correo = "nadie@uttt.edu.mx" });

        Assert.Equal((HttpStatusCode.Accepted, HttpStatusCode.Accepted), (existe.StatusCode, noExiste.StatusCode));
        Assert.Equal(await existe.Content.ReadAsStringAsync(), await noExiste.Content.ReadAsStringAsync());
        Assert.Equal(2, _correo.Enviados.Count);                         // el del registro y el reenviado
    }

    [Fact]
    public async Task Si_el_SMTP_falla_el_registro_igual_es_201_con_correoEnviado_false()
    {
        _correo.Error = new ServiceUnavailableException("El servicio de correo no está disponible en este momento.");

        var respuesta = await Cliente().PostAsJsonAsync("/api/auth/registro", Alumno());

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var json = (await respuesta.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.False((bool)json["correoEnviado"]!);
        Assert.Contains("reenviemos", (string?)json["mensaje"]);
    }

    [Theory]
    [InlineData("/api/auth/verificar-correo", "{\"token\":\"\"}", "Token")]
    [InlineData("/api/auth/reenviar-verificacion", "{\"correo\":\"no-es-correo\"}", "Correo")]
    public async Task Datos_invalidos_dan_400_por_campo(string ruta, string cuerpo, string campo)
    {
        var respuesta = await Cliente().PostAsync(ruta, new StringContent(cuerpo, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var errores = (await respuesta.Content.ReadFromJsonAsync<JsonObject>())!["errors"]!.AsObject();
        Assert.True(errores.ContainsKey(campo), errores.ToJsonString());
    }

    // Repositorio en memoria con las reglas de sp_registrar_usuario, sp_obtener_credenciales,
    // sp_generar_verificacion y sp_verificar_correo
    private sealed class UsuariosEnMemoria : IUsuarioRepository
    {
        private readonly Dictionary<string, (CredencialesUsuario Credenciales, string Nombre)> _porCorreo = new();
        private readonly Dictionary<string, string> _tokens = new();          // hash -> correo

        public Task<Guid> RegistrarAsync(string nombre, string correo, string contrasenaHash, string? matricula,
            string? telefono, CancellationToken cancellationToken = default)
        {
            var id = Guid.NewGuid();
            _porCorreo[correo] = (new CredencialesUsuario
            {
                IdUsuario = id, IdRol = RolUsuario.Alumno, ContrasenaHash = contrasenaHash, Activo = true
            }, nombre);
            return Task.FromResult(id);
        }

        public Task<CredencialesUsuario?> ObtenerCredencialesAsync(string correo, CancellationToken cancellationToken = default)
            => Task.FromResult(_porCorreo.TryGetValue(correo, out var u) ? u.Credenciales : null);

        public Task<string?> GenerarVerificacionAsync(
            string correo, string tokenHash, DateTimeOffset expira, CancellationToken cancellationToken = default)
        {
            if (!_porCorreo.TryGetValue(correo, out var u) || u.Credenciales.CorreoVerificado)
            {
                return Task.FromResult<string?>(null);
            }
            foreach (var anterior in _tokens.Where(t => t.Value == correo).Select(t => t.Key).ToList())
            {
                _tokens.Remove(anterior);
            }
            _tokens[tokenHash] = correo;
            return Task.FromResult<string?>(u.Nombre);
        }

        public Task<(Guid IdUsuario, string Correo, RolUsuario Rol)> VerificarCorreoAsync(
            string tokenHash, CancellationToken cancellationToken = default)
        {
            if (!_tokens.Remove(tokenHash, out var correo))
            {
                throw new BusinessRuleException("El enlace de verificación no es válido o ya se usó");
            }
            var credenciales = _porCorreo[correo].Credenciales;
            credenciales.CorreoVerificado = true;
            return Task.FromResult((credenciales.IdUsuario, correo, credenciales.IdRol));
        }

        public Task RestablecerPasswordAsync(string correo, string contrasenaHash, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Guid> RegistrarCafeteriaAsync(string nombreEncargado, string correo, string contrasenaHash, string? telefono,
            string nombreCafeteria, string ubicacion, bool esDelCampus, string puntoEntrega,
            IReadOnlyList<MetodoPago> metodos, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
