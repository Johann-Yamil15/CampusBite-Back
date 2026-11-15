using System.Net;
using System.Net.Http.Json;
using CampusBite.Domain.Auth;

namespace CampusBite.Api.Tests.BdReal;

// JJ-Sprint5 13/11/2026: órdenes del alumno y tablero del encargado contra la BD real
public class OrdenesConBdTests(BdDePrueba bd) : PruebaConBd(bd)
{
    [SkippableFact]
    public async Task Crear_orden_en_efectivo_y_verla_como_alumno_con_codigo_y_como_encargado_sin_el()
    {
        var id = await CrearOrdenAsync(cantidad: 2);

        var delAlumno = await Json(await ComoAlumno().GetAsync($"/api/ordenes/{id}"));
        Assert.Equal("Confirmada", (string?)delAlumno["estado"]);
        Assert.Equal("Pendiente", (string?)delAlumno["estadoPago"]);
        Assert.Equal(110m, (decimal)delAlumno["total"]!);
        Assert.Matches("^[0-9]{6}$", (string?)delAlumno["codigoRecogida"]);
        Assert.Null(delAlumno["alumno"]);
        Assert.Equal("Chilaquiles verdes", (string?)delAlumno["lineas"]![0]!["producto"]);

        var delEncargado = await Json(await ComoEncargado().GetAsync($"/api/ordenes/{id}"));
        Assert.Null(delEncargado["codigoRecogida"]);
        Assert.Equal("Alumno de Prueba", (string?)delEncargado["alumno"]!["nombre"]);

        // Otro alumno no la ve (403 de sp_exigir_gestor_cafeteria); una que no existe es 404
        Assert.Equal(HttpStatusCode.Forbidden, (await Cliente(RolUsuario.Alumno, Guid.NewGuid()).GetAsync($"/api/ordenes/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ComoAlumno().GetAsync($"/api/ordenes/{Guid.NewGuid()}")).StatusCode);
    }

    [SkippableFact]
    public async Task Validaciones_y_errores_de_negocio_al_crear()
    {
        var alumno = ComoAlumno();
        Assert.Equal(HttpStatusCode.BadRequest, (await alumno.PostAsJsonAsync("/api/ordenes", new { idCafeteria = Cafeteria })).StatusCode);

        var pasado = await alumno.PostAsJsonAsync("/api/ordenes", new
        {
            idCafeteria = Cafeteria, recogidaEn = DateTimeOffset.UtcNow, metodo = "Efectivo",
            items = new[] { new { idProducto = Chilaquiles, cantidad = 1 } }
        });
        Assert.Contains("5 minutos", await Detalle(pasado, HttpStatusCode.BadRequest));

        // El encargado no hace pedidos
        Assert.Equal(HttpStatusCode.Forbidden, (await ComoEncargado().PostAsJsonAsync("/api/ordenes", new { })).StatusCode);
    }

    [SkippableFact]
    public async Task Mis_ordenes_paginadas()
    {
        await CrearOrdenAsync();
        await CrearOrdenAsync(producto: Cafe);

        var pagina = await Json(await ComoAlumno().GetAsync("/api/ordenes/mias?pagina=1&tamanoPagina=1"));
        Assert.Single(pagina["items"]!.AsArray());
        Assert.True((int)pagina["total"]! >= 2);
        Assert.Equal(1, (int)pagina["tamanoPagina"]!);
        Assert.NotNull(pagina["items"]![0]!["codigoRecogida"]);
    }

    [SkippableFact]
    public async Task Flujo_del_tablero_preparar_lista_y_entregar_con_codigo()
    {
        var id = await CrearOrdenAsync();
        var codigo = (string)(await Json(await ComoAlumno().GetAsync($"/api/ordenes/{id}")))["codigoRecogida"]!;
        var encargado = ComoEncargado();

        var tablero = await Json(await encargado.GetAsync($"/api/cafeterias/{Cafeteria}/ordenes?fecha={FechaManana}"));
        Assert.Contains(tablero.AsArray(), o => (string?)o!["idOrden"] == id.ToString() && o["codigoRecogida"] is null);

        // Solo EnPreparacion o Lista por este endpoint
        Assert.Equal(HttpStatusCode.BadRequest,
            (await encargado.PatchAsJsonAsync($"/api/ordenes/{id}/estado", new { estado = "Entregada" })).StatusCode);

        Assert.Equal("EnPreparacion", (string?)(await Json(await encargado.PatchAsJsonAsync($"/api/ordenes/{id}/estado", new { estado = "EnPreparacion" })))["estado"]);
        Assert.Equal("Lista", (string?)(await Json(await encargado.PatchAsJsonAsync($"/api/ordenes/{id}/estado", new { estado = "Lista" })))["estado"]);

        var otroCodigo = codigo == "000000" ? "111111" : "000000";
        Assert.Contains("incorrecto", await Detalle(await encargado.PostAsJsonAsync($"/api/ordenes/{id}/entregar", new { codigoRecogida = otroCodigo }), HttpStatusCode.BadRequest));

        var entregada = await Json(await encargado.PostAsJsonAsync($"/api/ordenes/{id}/entregar", new { codigoRecogida = codigo }));
        Assert.Equal("Entregada", (string?)entregada["estado"]);
        Assert.Equal("Pagado", (string?)entregada["estadoPago"]);

        // Un alumno no usa el tablero
        Assert.Equal(HttpStatusCode.Forbidden, (await ComoAlumno().GetAsync($"/api/cafeterias/{Cafeteria}/ordenes")).StatusCode);
    }

    [SkippableFact]
    public async Task Cinco_codigos_erroneos_bloquean_la_entrega_con_429()
    {
        var id = await CrearOrdenAsync();
        var codigo = (string)(await Json(await ComoAlumno().GetAsync($"/api/ordenes/{id}")))["codigoRecogida"]!;
        var encargado = ComoEncargado();
        await encargado.PatchAsJsonAsync($"/api/ordenes/{id}/estado", new { estado = "EnPreparacion" });
        await encargado.PatchAsJsonAsync($"/api/ordenes/{id}/estado", new { estado = "Lista" });

        var malo = codigo == "000000" ? "111111" : "000000";
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await encargado.PostAsJsonAsync($"/api/ordenes/{id}/entregar", new { codigoRecogida = malo })).StatusCode);
        }

        // Bloqueada: ni el código correcto pasa
        Assert.Equal(HttpStatusCode.TooManyRequests,
            (await encargado.PostAsJsonAsync($"/api/ordenes/{id}/entregar", new { codigoRecogida = codigo })).StatusCode);
    }

    [SkippableFact]
    public async Task Transferencia_nace_pendiente_no_se_prepara_hasta_confirmar_el_pago()
    {
        var id = await CrearOrdenAsync("Transferencia");
        var encargado = ComoEncargado();

        Assert.Contains("confirmación del pago", await Detalle(
            await encargado.PatchAsJsonAsync($"/api/ordenes/{id}/estado", new { estado = "EnPreparacion" }), HttpStatusCode.BadRequest));

        var confirmada = await Json(await encargado.PostAsJsonAsync($"/api/ordenes/{id}/confirmar-pago", new { referencia = "SPEI 1234" }));
        Assert.Equal("Confirmada", (string?)confirmada["estado"]);
        Assert.Equal("Pagado", (string?)confirmada["estadoPago"]);

        // Ya no está pendiente de pago
        Assert.Equal(HttpStatusCode.BadRequest, (await encargado.PostAsJsonAsync($"/api/ordenes/{id}/confirmar-pago", new { })).StatusCode);
    }

    [SkippableFact]
    public async Task Rechazar_con_motivo_y_cancelar_como_alumno()
    {
        var encargado = ComoEncargado();
        var rechazada = await CrearOrdenAsync("Tarjeta");
        Assert.Equal(HttpStatusCode.BadRequest, (await encargado.PostAsJsonAsync($"/api/ordenes/{rechazada}/rechazar", new { motivo = "no" })).StatusCode);
        var orden = await Json(await encargado.PostAsJsonAsync($"/api/ordenes/{rechazada}/rechazar", new { motivo = "Se nos acabó el producto" }));
        Assert.Equal("Cancelada", (string?)orden["estado"]);
        Assert.Equal("Se nos acabó el producto", (string?)orden["motivoCancelacion"]);

        var cancelada = await CrearOrdenAsync();
        var delAlumno = await Json(await ComoAlumno().PostAsync($"/api/ordenes/{cancelada}/cancelar", null));
        Assert.Equal("Cancelado por el alumno", (string?)delAlumno["motivoCancelacion"]);

        // Una ya cancelada no se vuelve a cancelar; la de otro alumno es 404
        Assert.Equal(HttpStatusCode.BadRequest, (await ComoAlumno().PostAsync($"/api/ordenes/{cancelada}/cancelar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Cliente(RolUsuario.Alumno, Guid.NewGuid()).PostAsync($"/api/ordenes/{cancelada}/cancelar", null)).StatusCode);
    }

    [SkippableFact]
    public async Task Reportar_un_problema_una_sola_vez()
    {
        var id = await CrearOrdenAsync();
        var alumno = ComoAlumno();

        Assert.Equal(HttpStatusCode.BadRequest,
            (await alumno.PostAsJsonAsync($"/api/ordenes/{id}/reportes", new { motivo = "Otro", detalle = "corto" })).StatusCode);

        var reporte = await Json(await alumno.PostAsJsonAsync($"/api/ordenes/{id}/reportes", new { motivo = "FaltoArticulo", detalle = "Faltó el café" }), HttpStatusCode.Created);
        Assert.Matches("^R-[0-9]{6}$", (string?)reporte["folio"]);

        Assert.Contains("Ya reportaste", await Detalle(
            await alumno.PostAsJsonAsync($"/api/ordenes/{id}/reportes", new { motivo = "Calidad" }), HttpStatusCode.Conflict));
    }
}
