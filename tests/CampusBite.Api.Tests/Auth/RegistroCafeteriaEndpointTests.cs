using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CampusBite.Api.Tests.Seguridad;
using CampusBite.Application.Auth;
using CampusBite.Domain.Auth;
using CampusBite.Domain.Cafeterias;
using CampusBite.Domain.Common.Exceptions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CampusBite.Api.Tests.Auth;

// JJ-Sprint3 22/10/2026: TEC-11. POST /api/auth/registro-cafeteria con la API real en memoria: AuthService, BCrypt
// y JWT reales; solo el repositorio es falso (sin BD). Verifica acceso público, validación, 201 y 409.
public class RegistroCafeteriaEndpointTests : IClassFixture<ApiDePrueba>
{
    private const string Ruta = "/api/auth/registro-cafeteria";
    private readonly ApiDePrueba _api;
    private readonly RepositorioFalso _repositorio = new();

    public RegistroCafeteriaEndpointTests(ApiDePrueba api)
    {
        _api = api;
    }

    // Sin token: el endpoint es público
    private HttpClient Cliente() => _api.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
    {
        s.RemoveAll<IUsuarioRepository>();
        s.AddSingleton<IUsuarioRepository>(_repositorio);
    })).CreateClient();

    private static Dictionary<string, object?> Cuerpo() => new()
    {
        ["nombreEncargado"] = "Ana López",
        ["correo"] = "Ana@Negocio.mx",
        ["password"] = "Password123",
        ["telefono"] = null,
        ["nombreCafeteria"] = "Café Norte",
        ["ubicacion"] = "Edificio K",
        ["esDelCampus"] = true,
        ["puntoEntrega"] = "Ventanilla 2",
        ["metodosPago"] = new[] { "Efectivo", "Tarjeta" }
    };

    [Fact]
    public async Task Sin_sesion_crea_el_negocio_y_devuelve_201_con_rol_AdminCafeteria()
    {
        var respuesta = await Cliente().PostAsJsonAsync(Ruta, Cuerpo());

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var json = JsonNode.Parse(await respuesta.Content.ReadAsStringAsync())!;
        Assert.Equal("AdminCafeteria", (string?)json["rol"]);
        Assert.Equal("ana@negocio.mx", (string?)json["correo"]);
        Assert.False(string.IsNullOrEmpty((string?)json["token"]));
        Assert.Equal([MetodoPago.Efectivo, MetodoPago.Tarjeta], _repositorio.Metodos);
        Assert.NotEqual("Password123", _repositorio.Hash);                 // llega cifrada al repositorio
    }

    [Fact]
    public async Task El_token_devuelto_entra_como_AdminCafeteria()
    {
        var registro = JsonNode.Parse(await (await Cliente().PostAsJsonAsync(Ruta, Cuerpo())).Content.ReadAsStringAsync())!;
        var cliente = _api.ClienteConToken((string)registro["token"]!);

        var me = JsonNode.Parse(await cliente.GetStringAsync("/api/auth/me"))!;

        Assert.Equal(nameof(RolUsuario.AdminCafeteria), (string?)me["rol"]);
    }

    [Theory]
    [InlineData("esDelCampus", null, "EsDelCampus")]
    [InlineData("nombreCafeteria", "   ", "NombreCafeteria")]
    [InlineData("nombreEncargado", "A", "NombreEncargado")]
    [InlineData("password", "corta", "Password")]
    [InlineData("telefono", "abc", "Telefono")]
    [InlineData("correo", "no-es-correo", "Correo")]
    public async Task Datos_invalidos_dan_400_con_el_campo_en_errors(string campo, string? valor, string campoError)
    {
        var cuerpo = Cuerpo();
        cuerpo[campo] = valor;

        var respuesta = await Cliente().PostAsJsonAsync(Ruta, cuerpo);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var errores = JsonNode.Parse(await respuesta.Content.ReadAsStringAsync())!["errors"]!.AsObject();
        Assert.True(errores.ContainsKey(campoError), $"Se esperaba un error en {campoError}: {errores.ToJsonString()}");
        Assert.Null(_repositorio.Hash);                                     // no llegó a la BD
    }

    [Fact]
    public async Task Sin_metodos_de_pago_o_con_un_metodo_inexistente_da_400()
    {
        var sinMetodos = Cuerpo();
        sinMetodos["metodosPago"] = Array.Empty<string>();
        var metodoNumerico = Cuerpo();
        metodoNumerico["metodosPago"] = new[] { 7 };

        Assert.Equal(HttpStatusCode.BadRequest, (await Cliente().PostAsJsonAsync(Ruta, sinMetodos)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Cliente().PostAsJsonAsync(Ruta, metodoNumerico)).StatusCode);
    }

    [Fact]
    public async Task Correo_o_nombre_repetido_da_409_con_el_mensaje_de_la_BD()
    {
        _repositorio.Error = new ConflictException("Ya existe una cafetería con el nombre \"Café Norte\"");

        var respuesta = await Cliente().PostAsJsonAsync(Ruta, Cuerpo());

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        var detalle = (string?)JsonNode.Parse(await respuesta.Content.ReadAsStringAsync())!["detail"];
        Assert.Equal("Ya existe una cafetería con el nombre \"Café Norte\"", detalle);
    }

    // Solo implementa lo que usa el registro de negocio; lo demás no se llama en estas pruebas
    private sealed class RepositorioFalso : IUsuarioRepository
    {
        public Exception? Error { get; set; }
        public string? Hash { get; private set; }
        public IReadOnlyList<MetodoPago> Metodos { get; private set; } = [];

        public Task<Guid> RegistrarCafeteriaAsync(
            string nombreEncargado, string correo, string contrasenaHash, string? telefono,
            string nombreCafeteria, string ubicacion, bool esDelCampus, string puntoEntrega,
            IReadOnlyList<MetodoPago> metodos, CancellationToken cancellationToken = default)
        {
            if (Error is not null)
            {
                throw Error;
            }

            Hash = contrasenaHash;
            Metodos = metodos;
            return Task.FromResult(Guid.NewGuid());
        }

        public Task<Guid> RegistrarAsync(string nombre, string correo, string contrasenaHash, string? matricula,
            string? telefono, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<CredencialesUsuario?> ObtenerCredencialesAsync(string correo, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RestablecerPasswordAsync(string correo, string contrasenaHash, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
