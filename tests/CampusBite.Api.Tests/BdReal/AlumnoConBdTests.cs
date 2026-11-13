using System.Net;
using System.Net.Http.Json;
using CampusBite.Domain.Auth;

namespace CampusBite.Api.Tests.BdReal;

// JJ-Sprint5 13/11/2026: perfil, preferencias y favoritos del alumno contra la BD real
public class AlumnoConBdTests(BdDePrueba bd) : PruebaConBd(bd)
{
    [SkippableFact]
    public async Task Perfil_se_lee_y_se_actualiza()
    {
        var alumno = ComoAlumno();
        var perfil = await Json(await alumno.GetAsync("/api/alumnos/me/perfil"));
        Assert.Equal("alumno@ejemplo.test", (string?)perfil["correo"]);
        Assert.NotNull(perfil["preferencias"]!["avisoPedidoListo"]);   // el valor depende de la prueba de preferencias

        var datos = await Json(await alumno.PutAsJsonAsync("/api/alumnos/me/perfil",
            new { nombre = "  Alumno de Prueba  ", matricula = "UTT0000001", telefono = "7710000001" }));
        Assert.Equal("Alumno de Prueba", (string?)datos["nombre"]);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await alumno.PutAsJsonAsync("/api/alumnos/me/perfil", new { nombre = "A", telefono = "12" })).StatusCode);

        // Otro rol no tiene perfil de alumno
        Assert.Equal(HttpStatusCode.Forbidden, (await ComoEncargado().GetAsync("/api/alumnos/me/perfil")).StatusCode);
    }

    [SkippableFact]
    public async Task Preferencias_se_guardan_y_validan_la_cafeteria()
    {
        var alumno = ComoAlumno();
        var guardadas = await Json(await alumno.PutAsJsonAsync("/api/alumnos/me/preferencias",
            new { idCafeteriaFrecuente = Cafeteria, metodoPago = "Tarjeta", avisoPedidoListo = false, avisoPromociones = true }));
        Assert.Equal("Tarjeta", (string?)guardadas["metodoPago"]);

        var perfil = await Json(await alumno.GetAsync("/api/alumnos/me/perfil"));
        Assert.Equal(Cafeteria.ToString(), (string?)perfil["preferencias"]!["idCafeteriaFrecuente"]);
        Assert.False((bool)perfil["preferencias"]!["avisoPedidoListo"]!);

        Assert.Equal(HttpStatusCode.BadRequest, (await alumno.PutAsJsonAsync("/api/alumnos/me/preferencias",
            new { idCafeteriaFrecuente = Guid.NewGuid() })).StatusCode);
    }

    [SkippableFact]
    public async Task Favoritos_de_cafeterias_idempotentes()
    {
        var alumno = ComoAlumno();
        var favorito = await Json(await alumno.PutAsync($"/api/favoritos/cafeterias/{Cafeteria}", null));
        Assert.Equal(3, (int)favorito["platillos"]!);
        await Json(await alumno.PutAsync($"/api/favoritos/cafeterias/{Cafeteria}", null));   // repetir no es error

        Assert.Single((await Json(await alumno.GetAsync("/api/favoritos/cafeterias"))).AsArray());

        Assert.Equal(HttpStatusCode.NoContent, (await alumno.DeleteAsync($"/api/favoritos/cafeterias/{Cafeteria}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await alumno.DeleteAsync($"/api/favoritos/cafeterias/{Cafeteria}")).StatusCode);
        Assert.Empty((await Json(await alumno.GetAsync("/api/favoritos/cafeterias"))).AsArray());

        Assert.Equal(HttpStatusCode.NotFound, (await alumno.PutAsync($"/api/favoritos/cafeterias/{Guid.NewGuid()}", null)).StatusCode);
    }

    [SkippableFact]
    public async Task Favoritos_de_platillos_con_disponibilidad()
    {
        var alumno = Cliente(RolUsuario.Alumno, Alumno);
        var favorito = await Json(await alumno.PutAsync($"/api/favoritos/platillos/{Sandwich}", null));
        Assert.Equal("Sándwich de jamón", (string?)favorito["producto"]);
        Assert.True((bool)favorito["disponible"]!);

        var lista = await Json(await alumno.GetAsync("/api/favoritos/platillos"));
        Assert.Contains(lista.AsArray(), p => (string?)p!["idProducto"] == Sandwich.ToString());

        Assert.Equal(HttpStatusCode.NoContent, (await alumno.DeleteAsync($"/api/favoritos/platillos/{Sandwich}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await alumno.PutAsync($"/api/favoritos/platillos/{Guid.NewGuid()}", null)).StatusCode);
    }
}
