using System.Net;
using System.Net.Http.Json;
using CampusBite.Api.Tests.Auth;
using CampusBite.Application.Common.Correo;
using CampusBite.Domain.Auth;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace CampusBite.Api.Tests.BdReal;

// Seguimiento del pedido por correo de punta a punta: la API real con la BD real y un SMTP falso. Cada cambio de estado
// que hace el alumno o el encargado le manda un aviso al dueño de la orden, sin que esté en la página.
public class AvisosDePedidoConBdTests(BdDePrueba bd) : PruebaConBd(bd)
{
    private readonly CorreoFalso _correo = new();

    // Clientes de la misma API (con el mismo JWT) pero con el correo reemplazado por el falso
    private HttpClient Con(RolUsuario rol, Guid id)
    {
        Skip.If(Bd.Api is null, Bd.MotivoSinBd);
        var token = Bd.Api!.ClienteCon(rol, id).DefaultRequestHeaders.Authorization!.Parameter!;
        var cliente = Bd.Api.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IEnviadorCorreo>();
            s.AddSingleton<IEnviadorCorreo>(_correo);
        })).CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return cliente;
    }

    private async Task<Guid> PedirAsync(HttpClient alumno, string metodo)
    {
        var orden = await Json(await alumno.PostAsJsonAsync("/api/ordenes", new
        {
            idCafeteria = Cafeteria, recogidaEn = Manana(), metodo,
            items = new[] { new { idProducto = Chilaquiles, cantidad = 2 } }, notas = "Sin cebolla"
        }), HttpStatusCode.Created);
        return Guid.Parse((string)orden["idOrden"]!);
    }

    // Otras pruebas de BD real (AlumnoConBdTests) dejan apagado este aviso en el alumno compartido: cada prueba fija el suyo
    private Task FijarAvisoListoAsync(bool activo) => Bd.EjecutarAsync("""
        INSERT INTO preferencia_alumno (id_usuario, aviso_pedido_listo) VALUES (@id, @activo)
        ON CONFLICT (id_usuario) DO UPDATE SET aviso_pedido_listo = @activo
        """, new NpgsqlParameter("id", Alumno), new NpgsqlParameter("activo", activo));

    // El aviso corre en segundo plano: se espera a que llegue
    private async Task<(string Correo, string Nombre, AvisoPedido Aviso)> SiguienteAvisoAsync()
    {
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (_correo.AvisosPedido.IsEmpty)
        {
            await Task.Delay(20, limite.Token);
        }
        Assert.True(_correo.AvisosPedido.TryDequeue(out var aviso));
        return aviso;
    }

    [SkippableFact]
    public async Task Todo_el_flujo_de_un_pedido_avisa_al_alumno_en_cada_cambio_de_estado()
    {
        var alumno = Con(RolUsuario.Alumno, Alumno);
        var encargado = Con(RolUsuario.AdminCafeteria, Encargado);
        await FijarAvisoListoAsync(true);

        var id = await PedirAsync(alumno, "Transferencia");
        Assert.True(_correo.AvisosPedido.IsEmpty);                                  // crear no avisa: empieza al aceptar

        await Json(await encargado.PostAsJsonAsync($"/api/ordenes/{id}/confirmar-pago", new { referencia = "SPEI 1234" }));
        var aceptada = await SiguienteAvisoAsync();
        Assert.Equal(("alumno@ejemplo.test", "Alumno de Prueba"), (aceptada.Correo, aceptada.Nombre));
        Assert.Equal((EventoPedido.PagoConfirmado, id, 110m), (aceptada.Aviso.Evento, aceptada.Aviso.IdOrden, aceptada.Aviso.Total));
        Assert.Equal("Chilaquiles verdes", Assert.Single(aceptada.Aviso.Lineas).Producto);
        Assert.Null(aceptada.Aviso.CodigoRecogida);

        await Json(await encargado.PatchAsJsonAsync($"/api/ordenes/{id}/estado", new { estado = "EnPreparacion" }));
        Assert.Equal(EventoPedido.EnPreparacion, (await SiguienteAvisoAsync()).Aviso.Evento);

        await Json(await encargado.PatchAsJsonAsync($"/api/ordenes/{id}/estado", new { estado = "Lista" }));
        var lista = await SiguienteAvisoAsync();
        var codigoDelAlumno = (string)(await Json(await alumno.GetAsync($"/api/ordenes/{id}")))["codigoRecogida"]!;
        Assert.Equal(EventoPedido.Lista, lista.Aviso.Evento);
        Assert.Equal(codigoDelAlumno, lista.Aviso.CodigoRecogida);                  // el correo lleva el código que ve el alumno

        await Json(await encargado.PostAsJsonAsync($"/api/ordenes/{id}/entregar", new { codigoRecogida = codigoDelAlumno }));
        var entregada = await SiguienteAvisoAsync();
        Assert.Equal(EventoPedido.Entregada, entregada.Aviso.Evento);
        Assert.Null(entregada.Aviso.CodigoRecogida);
        Assert.True(_correo.AvisosPedido.IsEmpty);
    }

    [SkippableFact]
    public async Task Rechazar_y_cancelar_avisan_con_el_motivo_de_la_cafeteria()
    {
        var alumno = Con(RolUsuario.Alumno, Alumno);
        var encargado = Con(RolUsuario.AdminCafeteria, Encargado);
        var rechazada = await PedirAsync(alumno, "Tarjeta");

        await Json(await encargado.PostAsJsonAsync($"/api/ordenes/{rechazada}/rechazar", new { motivo = "Se nos acabó el producto" }));

        var aviso = (await SiguienteAvisoAsync()).Aviso;
        Assert.Equal((EventoPedido.Cancelada, "Se nos acabó el producto"), (aviso.Evento, aviso.MotivoCancelacion));

        var propia = await PedirAsync(alumno, "Efectivo");
        await Json(await alumno.PostAsync($"/api/ordenes/{propia}/cancelar", content: null));
        Assert.Equal(EventoPedido.Cancelada, (await SiguienteAvisoAsync()).Aviso.Evento);
    }

    [SkippableFact]
    public async Task Si_el_SMTP_falla_la_orden_cambia_de_estado_igual_y_el_cliente_recibe_su_respuesta()
    {
        var alumno = Con(RolUsuario.Alumno, Alumno);
        var encargado = Con(RolUsuario.AdminCafeteria, Encargado);
        _correo.Error = new CampusBite.Domain.Common.Exceptions.ServiceUnavailableException("SMTP caído");

        var id = await PedirAsync(alumno, "Efectivo");                              // 201: la orden se creó
        var enPreparacion = await Json(await encargado.PatchAsJsonAsync($"/api/ordenes/{id}/estado", new { estado = "EnPreparacion" }));

        Assert.Equal("EnPreparacion", (string?)enPreparacion["estado"]);
        await Task.Delay(300);                                                       // el aviso fallido corre en segundo plano
        Assert.True(_correo.AvisosPedido.IsEmpty);
    }

    [SkippableFact]
    public async Task Un_alumno_que_apago_el_aviso_de_pedido_listo_no_recibe_ese_correo_pero_si_los_demas()
    {
        var alumno = Con(RolUsuario.Alumno, Alumno);
        var encargado = Con(RolUsuario.AdminCafeteria, Encargado);
        await FijarAvisoListoAsync(false);
        try
        {
            var id = await PedirAsync(alumno, "Efectivo");

            await Json(await encargado.PatchAsJsonAsync($"/api/ordenes/{id}/estado", new { estado = "EnPreparacion" }));
            Assert.Equal(EventoPedido.EnPreparacion, (await SiguienteAvisoAsync()).Aviso.Evento);

            await Json(await encargado.PatchAsJsonAsync($"/api/ordenes/{id}/estado", new { estado = "Lista" }));
            await Task.Delay(300);
            Assert.True(_correo.AvisosPedido.IsEmpty);                              // no hay correo de "lista"
        }
        finally
        {
            await FijarAvisoListoAsync(true);
        }
    }
}
