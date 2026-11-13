using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CampusBite.Domain.Auth;

namespace CampusBite.Api.Tests.BdReal;

// JJ-Sprint5 13/11/2026: base de las pruebas con BD real: ids de 90_datos_dev/002_datos_prueba.sql y utilidades
[Collection(ColeccionBdReal.Nombre)]
public abstract class PruebaConBd(BdDePrueba bd)
{
    protected static readonly Guid Alumno = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    protected static readonly Guid Encargado = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
    protected static readonly Guid AdminSistema = Guid.Parse("00000000-0000-0000-0000-0000000000a3");
    protected static readonly Guid Cafeteria = Guid.Parse("00000000-0000-0000-0000-0000000000c1");
    protected static readonly Guid Chilaquiles = Guid.Parse("00000000-0000-0000-0000-0000000000e1");
    protected static readonly Guid Sandwich = Guid.Parse("00000000-0000-0000-0000-0000000000e2");
    protected static readonly Guid Cafe = Guid.Parse("00000000-0000-0000-0000-0000000000e3");

    protected BdDePrueba Bd { get; } = bd;

    // La cafetería de prueba abre de 07:00 a 20:00 todos los días; mañana a esta hora siempre está abierta
    protected static DateTimeOffset Manana(int hora = 13) =>
        new(DateTime.SpecifyKind(DateTime.UtcNow.AddHours(-6).Date.AddDays(1).AddHours(hora), DateTimeKind.Unspecified),
            TimeSpan.FromHours(-6));

    protected static string FechaManana => Manana().ToString("yyyy-MM-dd");

    protected HttpClient Cliente(RolUsuario rol, Guid id)
    {
        Skip.If(Bd.Api is null, Bd.MotivoSinBd);
        return Bd.Api!.ClienteCon(rol, id);
    }

    protected HttpClient ComoAlumno() => Cliente(RolUsuario.Alumno, Alumno);
    protected HttpClient ComoEncargado() => Cliente(RolUsuario.AdminCafeteria, Encargado);

    protected static async Task<JsonNode> Json(HttpResponseMessage respuesta, HttpStatusCode esperado = HttpStatusCode.OK)
    {
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.True(respuesta.StatusCode == esperado, $"Se esperaba {(int)esperado} y llegó {(int)respuesta.StatusCode}: {cuerpo}");
        return JsonNode.Parse(cuerpo)!;
    }

    protected static async Task<string> Detalle(HttpResponseMessage respuesta, HttpStatusCode esperado)
    {
        var problema = await Json(respuesta, esperado);
        return (string?)problema["detail"] ?? string.Empty;
    }

    // Crea una orden del alumno para mañana y devuelve su id
    protected async Task<Guid> CrearOrdenAsync(string metodo = "Efectivo", Guid? producto = null, int cantidad = 1)
    {
        var orden = await Json(await ComoAlumno().PostAsJsonAsync("/api/ordenes", new
        {
            idCafeteria = Cafeteria,
            recogidaEn = Manana(),
            metodo,
            items = new[] { new { idProducto = producto ?? Chilaquiles, cantidad } },
            notas = "Sin cebolla"
        }), HttpStatusCode.Created);
        return Guid.Parse((string)orden["idOrden"]!);
    }
}
