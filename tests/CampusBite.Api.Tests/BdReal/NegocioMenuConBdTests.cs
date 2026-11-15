using System.Net;
using System.Net.Http.Json;
using CampusBite.Application.Common.Security;
using CampusBite.Domain.Auth;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CampusBite.Api.Tests.BdReal;

// JJ-Sprint5 13/11/2026: horario, promociones, estadísticas, menú y auth contra la BD real
public class NegocioMenuConBdTests(BdDePrueba bd) : PruebaConBd(bd)
{
    private static object[] Semana(string desde = "07:00", string hasta = "20:00") =>
        Enumerable.Range(0, 7).Select(_ => (object)new { abre = true, desde, hasta }).ToArray();

    [SkippableFact]
    public async Task Horario_semanal_y_cerrar_por_hoy()
    {
        var encargado = ComoEncargado();
        var horario = await Json(await encargado.GetAsync($"/api/cafeterias/{Cafeteria}/horario"));
        Assert.Equal(7, horario["dias"]!.AsArray().Count);
        Assert.Equal("07:00", (string?)horario["dias"]![0]!["desde"]);

        // Se vuelve a guardar el mismo horario (las demás pruebas lo necesitan)
        var guardado = await Json(await encargado.PutAsJsonAsync($"/api/cafeterias/{Cafeteria}/horario", new { dias = Semana() }));
        Assert.Equal("20:00", (string?)guardado["dias"]![6]!["hasta"]);

        Assert.Contains("Lunes", await Detalle(await encargado.PutAsJsonAsync($"/api/cafeterias/{Cafeteria}/horario",
            new { dias = Semana("18:00", "09:00") }), HttpStatusCode.BadRequest));
        Assert.Equal(HttpStatusCode.BadRequest,
            (await encargado.PutAsJsonAsync($"/api/cafeterias/{Cafeteria}/horario", new { dias = Semana().Take(3) })).StatusCode);

        var cerrada = await Json(await encargado.PostAsync($"/api/cafeterias/{Cafeteria}/horario/cierre-hoy", null));
        Assert.True((bool)cerrada["cerradoHoy"]!);
        Assert.False((bool)cerrada["abiertaAhora"]!);
        var abierta = await Json(await encargado.DeleteAsync($"/api/cafeterias/{Cafeteria}/horario/cierre-hoy"));
        Assert.False((bool)abierta["cerradoHoy"]!);

        Assert.Equal(HttpStatusCode.NotFound, (await Cliente(RolUsuario.AdminSistema, AdminSistema).GetAsync($"/api/cafeterias/{Guid.NewGuid()}/horario")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ComoAlumno().GetAsync($"/api/cafeterias/{Cafeteria}/horario")).StatusCode);
    }

    [SkippableFact]
    public async Task Promociones_crud_y_activas_para_el_alumno()
    {
        var encargado = ComoEncargado();
        var hoy = DateTime.UtcNow.AddHours(-6).ToString("yyyy-MM-dd");
        var creada = await Json(await encargado.PostAsJsonAsync($"/api/cafeterias/{Cafeteria}/promociones", new
        {
            nombre = "Martes de café", descripcion = "20% en bebidas", tipo = "Porcentaje", valor = 20, desde = hoy, hasta = hoy, activa = true
        }), HttpStatusCode.Created);
        var id = (string)creada["id"]!;
        Assert.Equal(0, (int)creada["usos"]!);

        var activas = await Json(await ComoAlumno().GetAsync("/api/promociones/activas"));
        Assert.Contains(activas.AsArray(), p => (string?)p!["etiqueta"] == "-20%" && (string?)p["subtitulo"] == "20% en bebidas · Cafetería Central (prueba)");

        var editada = await Json(await encargado.PutAsJsonAsync($"/api/cafeterias/{Cafeteria}/promociones/{id}", new
        {
            nombre = "Combo desayuno", tipo = "Combo", valor = 79.5, desde = hoy, hasta = hoy, activa = false
        }));
        Assert.Equal("Combo", (string?)editada["tipo"]);
        Assert.Single((await Json(await encargado.GetAsync($"/api/cafeterias/{Cafeteria}/promociones"))).AsArray());

        Assert.Contains("entero", await Detalle(await encargado.PostAsJsonAsync($"/api/cafeterias/{Cafeteria}/promociones", new
        {
            nombre = "Mal", tipo = "Porcentaje", valor = 95, desde = hoy, hasta = hoy
        }), HttpStatusCode.BadRequest));

        Assert.Equal(HttpStatusCode.NoContent, (await encargado.DeleteAsync($"/api/cafeterias/{Cafeteria}/promociones/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await encargado.DeleteAsync($"/api/cafeterias/{Cafeteria}/promociones/{id}")).StatusCode);
    }

    [SkippableFact]
    public async Task Estadisticas_por_periodo()
    {
        await CrearOrdenAsync();
        var encargado = ComoEncargado();

        var semana = await Json(await encargado.GetAsync($"/api/cafeterias/{Cafeteria}/estadisticas?periodo=7d"));
        Assert.Equal(7, semana["ventasPorDia"]!.AsArray().Count);
        Assert.True((int)semana["pedidos"]! >= 1);
        Assert.NotEmpty(semana["masVendidos"]!.AsArray());

        await Json(await encargado.GetAsync($"/api/cafeterias/{Cafeteria}/estadisticas"));
        Assert.Equal(HttpStatusCode.BadRequest, (await encargado.GetAsync($"/api/cafeterias/{Cafeteria}/estadisticas?periodo=1a")).StatusCode);
    }

    [SkippableFact]
    public async Task Menu_completo_por_cafeteria_y_busqueda()
    {
        var alumno = ComoAlumno();
        var menu = await Json(await alumno.GetAsync("/api/menu"));
        var central = menu.AsArray().Single(c => (string?)c!["idCafeteria"] == Cafeteria.ToString())!;
        Assert.Equal("20:00", (string?)central["cierraA"]);
        Assert.Equal(3, central["productos"]!.AsArray().Count);
        Assert.NotNull(central["productos"]![0]!["dietas"]);

        var una = await Json(await alumno.GetAsync($"/api/menu/{Cafeteria}"));
        Assert.Equal(3, una["metodosPago"]!.AsArray().Count);
        Assert.Equal(HttpStatusCode.NotFound, (await alumno.GetAsync($"/api/menu/{Guid.NewGuid()}")).StatusCode);

        var busqueda = await Json(await alumno.GetAsync("/api/menu/buscar?texto=cafe&orden=PrecioAsc&tamanoPagina=2"));
        Assert.Equal(2, busqueda["items"]!.AsArray().Count);
    }

    [SkippableFact]
    public async Task Login_devuelve_nombre_y_cafeteria_me_los_lee_del_token_y_cambiar_password()
    {
        Skip.If(Bd.Api is null, Bd.MotivoSinBd);
        string hash;
        using (var scope = Bd.Api!.Services.CreateScope())
        {
            hash = scope.ServiceProvider.GetRequiredService<IPasswordHasher>().Hash("Actual12345");
        }
        await Bd.EjecutarAsync("UPDATE usuario SET contrasena_hash = @h WHERE id_usuario = @id",
            new NpgsqlParameter("h", hash), new NpgsqlParameter("id", Encargado));

        var anonimo = Bd.Api.CreateClient();
        var sesion = await Json(await anonimo.PostAsJsonAsync("/api/auth/login", new { correo = "cafeteria@ejemplo.test", password = "Actual12345" }));
        Assert.Equal(Cafeteria.ToString(), (string?)sesion["idCafeteria"]);
        Assert.False(string.IsNullOrEmpty((string?)sesion["nombre"]));

        var conToken = Bd.Api.ClienteConToken((string)sesion["token"]!);
        var me = await Json(await conToken.GetAsync("/api/auth/me"));
        Assert.Equal(Cafeteria.ToString(), (string?)me["idCafeteria"]);
        Assert.Equal((string?)sesion["nombre"], (string?)me["nombre"]);

        Assert.Contains("actual no es correcta", await Detalle(await conToken.PostAsJsonAsync("/api/auth/cambiar-password",
            new { passwordActual = "Otra123456", passwordNueva = "Nueva12345" }), HttpStatusCode.BadRequest));
        Assert.Equal(HttpStatusCode.NoContent, (await conToken.PostAsJsonAsync("/api/auth/cambiar-password",
            new { passwordActual = "Actual12345", passwordNueva = "Nueva12345" })).StatusCode);
    }
}
