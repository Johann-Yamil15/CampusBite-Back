using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CampusBite.Api.Tests.Seguridad;
using CampusBite.Application.Cafeterias;
using CampusBite.Application.Cafeterias.Dtos;
using CampusBite.Domain.Auth;
using CampusBite.Domain.Cafeterias;
using CampusBite.Domain.Common.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CampusBite.Api.Tests.Cafeterias;

// JY-Sprint3 20/10/2026: TEC-08. Endpoints de cafeterías con la API real en memoria y un servicio falso (sin BD):
// permisos por rol, validación, formato JSON y traducción de errores a HTTP.
public class CafeteriasEndpointsTests : IClassFixture<ApiDePrueba>
{
    private static readonly Guid IdExistente = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly ApiDePrueba _api;
    private readonly ServicioFalso _servicio = new();

    public CafeteriasEndpointsTests(ApiDePrueba api)
    {
        _api = api;
    }

    private HttpClient Cliente(RolUsuario rol)
    {
        var fabrica = _api.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<ICafeteriaService>();
            s.AddSingleton<ICafeteriaService>(_servicio);
        }));
        var (token, _) = fabrica.Services.CreateScope().ServiceProvider
            .GetRequiredService<CampusBite.Application.Common.Security.ITokenService>()
            .GenerarToken(Guid.NewGuid(), "prueba@ejemplo.test", rol);
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", token);
        return cliente;
    }

    private static object Cuerpo(params string[] metodos) => new
    {
        nombre = "Cafetería Norte", ubicacion = "Edificio K", esDelCampus = true, puntoEntrega = "Ventanilla 2",
        metodosPago = metodos.Length > 0 ? metodos : ["Efectivo"]
    };

    // Quién puede hacer qué (el servicio falso siempre tiene éxito: lo que se prueba es la política)
    [Theory]
    [InlineData(RolUsuario.Alumno,         "POST",   HttpStatusCode.Forbidden)]
    [InlineData(RolUsuario.AdminCafeteria, "POST",   HttpStatusCode.Forbidden)]
    [InlineData(RolUsuario.AdminSistema,   "POST",   HttpStatusCode.Created)]
    [InlineData(RolUsuario.Alumno,         "PUT",    HttpStatusCode.Forbidden)]
    [InlineData(RolUsuario.AdminCafeteria, "PUT",    HttpStatusCode.OK)]
    [InlineData(RolUsuario.AdminSistema,   "PUT",    HttpStatusCode.OK)]
    [InlineData(RolUsuario.Alumno,         "DELETE", HttpStatusCode.Forbidden)]
    [InlineData(RolUsuario.AdminCafeteria, "DELETE", HttpStatusCode.Forbidden)]
    [InlineData(RolUsuario.AdminSistema,   "DELETE", HttpStatusCode.NoContent)]
    [InlineData(RolUsuario.AdminCafeteria, "REACTIVAR", HttpStatusCode.Forbidden)]
    [InlineData(RolUsuario.AdminSistema,   "REACTIVAR", HttpStatusCode.NoContent)]
    [InlineData(RolUsuario.Alumno,         "GET",    HttpStatusCode.OK)]
    public async Task Cada_rol_solo_hace_lo_que_le_toca(RolUsuario rol, string accion, HttpStatusCode esperado)
    {
        var cliente = Cliente(rol);
        var ruta = $"/api/cafeterias/{IdExistente}";

        var respuesta = accion switch
        {
            "POST" => await cliente.PostAsJsonAsync("/api/cafeterias", Cuerpo()),
            "PUT" => await cliente.PutAsJsonAsync(ruta, Cuerpo()),
            "DELETE" => await cliente.DeleteAsync(ruta),
            "REACTIVAR" => await cliente.PostAsync($"{ruta}/reactivar", null),
            _ => await cliente.GetAsync("/api/cafeterias")
        };

        Assert.Equal(esperado, respuesta.StatusCode);
    }

    [Fact]
    public async Task Sin_sesion_no_se_puede_ni_listar()
    {
        var respuesta = await _api.CreateClient().GetAsync("/api/cafeterias");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Crear_responde_201_con_Location_y_metodos_como_texto()
    {
        var respuesta = await Cliente(RolUsuario.AdminSistema).PostAsJsonAsync("/api/cafeterias", Cuerpo("efectivo", "Tarjeta"));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        Assert.EndsWith($"/api/cafeterias/{IdExistente}", respuesta.Headers.Location?.ToString(), StringComparison.OrdinalIgnoreCase);
        var json = JsonNode.Parse(await respuesta.Content.ReadAsStringAsync())!;
        Assert.Equal("Efectivo", json["metodosPago"]![0]!.GetValue<string>());
        Assert.Equal([MetodoPago.Efectivo, MetodoPago.Tarjeta], _servicio.UltimoDto!.MetodosPago);
    }

    [Theory]
    [InlineData("""{"nombre":"Cafetería","ubicacion":"K","esDelCampus":true,"puntoEntrega":"V","metodosPago":[]}""", "MetodosPago")]
    [InlineData("""{"nombre":"Cafetería","ubicacion":"K","puntoEntrega":"V","metodosPago":["Efectivo"]}""", "EsDelCampus")]
    [InlineData("""{"nombre":" ","ubicacion":"K","esDelCampus":true,"puntoEntrega":"V","metodosPago":["Efectivo"]}""", "Nombre")]
    [InlineData("""{"nombre":"Cafetería","ubicacion":"   ","esDelCampus":true,"puntoEntrega":"V","metodosPago":["Efectivo"]}""", "Ubicacion")]
    public async Task Datos_invalidos_responden_400_y_no_llegan_al_servicio(string cuerpo, string campo)
    {
        var respuesta = await Cliente(RolUsuario.AdminSistema).PostAsync("/api/cafeterias",
            new StringContent(cuerpo, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var problema = await respuesta.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains(problema!.Errors.Keys, k => k.Equals(campo, StringComparison.OrdinalIgnoreCase));
        Assert.Null(_servicio.UltimoDto);
    }

    [Theory]
    [InlineData("""["Bitcoin"]""")]   // no existe
    [InlineData("""[3]""")]           // número en lugar de texto
    public async Task Metodo_de_pago_no_valido_responde_400(string metodos)
    {
        var cuerpo = $$"""{"nombre":"Cafetería","ubicacion":"K","esDelCampus":true,"puntoEntrega":"V","metodosPago":{{metodos}}}""";

        var respuesta = await Cliente(RolUsuario.AdminSistema).PostAsync("/api/cafeterias",
            new StringContent(cuerpo, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Null(_servicio.UltimoDto);
    }

    // Lo que dice la BD llega al front con el código correcto (ExceptionMiddleware)
    [Theory]
    [InlineData("NotFound", HttpStatusCode.NotFound)]
    [InlineData("Forbidden", HttpStatusCode.Forbidden)]
    [InlineData("Conflict", HttpStatusCode.Conflict)]
    public async Task Errores_del_servicio_se_traducen_a_HTTP(string error, HttpStatusCode esperado)
    {
        _servicio.Error = error switch
        {
            "NotFound" => new NotFoundException("La cafetería no existe."),
            "Forbidden" => new ForbiddenException("Sin permiso para editar esta cafetería"),
            _ => new ConflictException("La cafetería tiene 2 orden(es) en curso")
        };

        var respuesta = await Cliente(RolUsuario.AdminCafeteria).PutAsJsonAsync($"/api/cafeterias/{IdExistente}", Cuerpo());

        Assert.Equal(esperado, respuesta.StatusCode);
        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(_servicio.Error.Message, problema?.Detail);
    }

    [Fact]
    public async Task Un_id_que_no_es_guid_responde_404_sin_llegar_al_servicio()
    {
        var respuesta = await Cliente(RolUsuario.AdminSistema).GetAsync("/api/cafeterias/no-es-un-id");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    private sealed class ServicioFalso : ICafeteriaService
    {
        public Exception? Error { get; set; }
        public GuardarCafeteriaDto? UltimoDto { get; private set; }

        private static CafeteriaDto Cafeteria(GuardarCafeteriaDto? dto = null) => new()
        {
            IdCafeteria = IdExistente, Nombre = dto?.Nombre ?? "Cafetería Central", Ubicacion = "Edificio A",
            EsDelCampus = true, PuntoEntrega = "Puerta 1", Activa = true,
            MetodosPago = dto?.MetodosPago ?? [MetodoPago.Efectivo]
        };

        private Task<T> Responder<T>(T valor) => Error is null ? Task.FromResult(valor) : Task.FromException<T>(Error);

        public Task<IReadOnlyList<CafeteriaDto>> ListarAsync(bool incluirInactivas, CancellationToken ct = default) =>
            Responder<IReadOnlyList<CafeteriaDto>>([Cafeteria()]);
        public Task<CafeteriaDto> ObtenerAsync(Guid idCafeteria, CancellationToken ct = default) => Responder(Cafeteria());
        public Task<CafeteriaDto> CrearAsync(GuardarCafeteriaDto dto, CancellationToken ct = default)
        {
            UltimoDto = dto;
            return Responder(Cafeteria(dto));
        }
        public Task<CafeteriaDto> ActualizarAsync(Guid idCafeteria, GuardarCafeteriaDto dto, CancellationToken ct = default)
        {
            UltimoDto = dto;
            return Responder(Cafeteria(dto));
        }
        public Task DesactivarAsync(Guid idCafeteria, CancellationToken ct = default) => Responder(true);
        public Task ReactivarAsync(Guid idCafeteria, CancellationToken ct = default) => Responder(true);
    }
}
