using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CampusBite.Api.Tests.Seguridad;
using CampusBite.Application.Common.Security;
using CampusBite.Domain.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CampusBite.Api.Tests.Postgres;

// La API completa (controlador -> servicio -> repositorio -> procedimiento) contra el PostgreSQL desechable:
// códigos HTTP reales que salen de los SQLSTATE reales, sin dobles de prueba en medio.
[Collection(ColeccionPostgres.Nombre)]
public class ApiConPostgresTests(PostgresDePrueba bd) : IClassFixture<ApiDePrueba>
{
    private WebApplicationFactory<Program> Api(ApiDePrueba api) =>
        api.WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:Database", bd.Conexion));

    private static HttpClient Como(WebApplicationFactory<Program> api, RolUsuario rol, Guid id)
    {
        using var scope = api.Services.CreateScope();
        var (token, _) = scope.ServiceProvider.GetRequiredService<ITokenService>().GenerarToken(id, "prueba@ejemplo.test", rol);
        var cliente = api.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return cliente;
    }

    [FactPostgres]
    public async Task Obtener_una_cafeteria_devuelve_los_datos_reales()
    {
        var api = Api(new ApiDePrueba());

        var respuesta = await Como(api, RolUsuario.Alumno, PostgresDePrueba.Alumno).GetAsync($"/api/cafeterias/{PostgresDePrueba.Cafeteria}");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var c = (await respuesta.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal(PostgresDePrueba.Cafeteria.ToString(), (string?)c["idCafeteria"]);
        Assert.Equal("Cafetería Central (prueba)", (string?)c["nombre"]);
        Assert.Equal("Puerta 1", (string?)c["puntoEntrega"]);
        Assert.Equal(3, c["metodosPago"]!.AsArray().Count);
    }

    [FactPostgres]
    public async Task Una_cafeteria_que_no_existe_es_404_con_ProblemDetails()
    {
        var api = Api(new ApiDePrueba());

        var respuesta = await Como(api, RolUsuario.Alumno, PostgresDePrueba.Alumno).GetAsync($"/api/cafeterias/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Equal("application/problem+json", respuesta.Content.Headers.ContentType?.MediaType);
    }

    [FactPostgres]
    public async Task Crear_cafeteria_como_admin_da_201_y_repetida_409_desde_la_BD()
    {
        var api = Api(new ApiDePrueba());
        var admin = Como(api, RolUsuario.AdminSistema, PostgresDePrueba.Admin);
        var cuerpo = new
        {
            nombre = $"HTTP {Guid.NewGuid().ToString("N")[..8]}", ubicacion = "Edificio D", esDelCampus = true,
            puntoEntrega = "Caja", metodosPago = new[] { "Efectivo" }
        };

        var creada = await admin.PostAsJsonAsync("/api/cafeterias", cuerpo);
        var repetida = await admin.PostAsJsonAsync("/api/cafeterias", cuerpo);

        Assert.Equal(HttpStatusCode.Created, creada.StatusCode);
        Assert.NotNull(creada.Headers.Location);
        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);
        Assert.Contains("Ya existe una cafetería", (string?)(await repetida.Content.ReadFromJsonAsync<JsonObject>())!["detail"]);
    }

    [FactPostgres]
    public async Task Un_encargado_no_puede_editar_una_cafeteria_ajena_403_desde_la_BD()
    {
        var api = Api(new ApiDePrueba());
        var admin = Como(api, RolUsuario.AdminSistema, PostgresDePrueba.Admin);
        var cuerpo = new
        {
            nombre = $"Ajena {Guid.NewGuid().ToString("N")[..8]}", ubicacion = "E", esDelCampus = false,
            puntoEntrega = "P", metodosPago = new[] { "Tarjeta" }
        };
        var location = (await admin.PostAsJsonAsync("/api/cafeterias", cuerpo)).Headers.Location!;

        var respuesta = await Como(api, RolUsuario.AdminCafeteria, PostgresDePrueba.Encargado).PutAsJsonAsync(location, cuerpo);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }
}
