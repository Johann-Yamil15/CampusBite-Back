using CampusBite.Application.Ordenes.Dtos;
using CampusBite.Domain.Cafeterias;
using CampusBite.Domain.Common.Exceptions;
using CampusBite.Infrastructure.Auth;
using CampusBite.Infrastructure.Ordenes;
using Npgsql;

namespace CampusBite.Api.Tests.Postgres;

// sp_destinatario_orden contra la base real: a quién se le avisa por correo de una orden y con qué permisos.
[Collection(ColeccionPostgres.Nombre)]
public class DestinatarioOrdenTests(PostgresDePrueba bd)
{
    private OrdenRepository Ordenes() => new(bd.Contexto());

    // La cafetería de prueba abre de 07:00 a 20:00 todos los días: mañana a las 13:00 (hora del centro de México)
    private static DateTimeOffset Manana()
    {
        var hoy = DateTime.UtcNow.AddHours(-6).Date;
        return new DateTimeOffset(DateTime.SpecifyKind(hoy.AddDays(1).AddHours(13), DateTimeKind.Unspecified), TimeSpan.FromHours(-6));
    }

    private Task<Guid> CrearOrdenAsync(Guid idAlumno) => Ordenes().CrearAsync(idAlumno, PostgresDePrueba.Cafeteria, Manana(),
        MetodoPago.Efectivo, [new ItemOrdenDto { IdProducto = PostgresDePrueba.Chilaquiles, Cantidad = 1 }], null);

    // Un alumno nuevo con correo verificado (cada prueba usa el suyo: no toca a los usuarios compartidos)
    private async Task<(Guid Id, string Correo)> NuevoAlumnoAsync()
    {
        var correo = $"aviso_{Guid.NewGuid():N}@ejemplo.test";
        var id = await new UsuarioRepository(bd.Contexto()).RegistrarAsync("Alumno Aviso", correo, "$2a$11$hash.de.prueba.no.real.xxxxxxxxxxxxxx", null, null);
        await bd.EjecutarAsync("UPDATE usuario SET correo_verificado = true WHERE id_usuario = @id", new NpgsqlParameter("id", id));
        return (id, correo);
    }

    [FactPostgres]
    public async Task El_dueno_de_la_orden_es_el_destinatario_con_su_codigo_de_recogida()
    {
        var idOrden = await CrearOrdenAsync(PostgresDePrueba.Alumno);

        var destinatario = await Ordenes().DestinatarioAsync(idOrden, PostgresDePrueba.Alumno);

        Assert.NotNull(destinatario);
        Assert.Equal((PostgresDePrueba.Alumno, "alumno@ejemplo.test", "Alumno de Prueba", true),
            (destinatario.IdUsuario, destinatario.Correo, destinatario.Nombre, destinatario.AvisoPedidoListo));
        Assert.Matches("^[0-9]{6}$", destinatario.CodigoRecogida);
        var suOrden = await Ordenes().ObtenerAsync(idOrden, PostgresDePrueba.Alumno);
        Assert.Equal(suOrden.CodigoRecogida, destinatario.CodigoRecogida);       // el mismo que ve el alumno en la app
    }

    [FactPostgres]
    public async Task El_encargado_de_la_cafeteria_obtiene_al_mismo_destinatario()
    {
        var idOrden = await CrearOrdenAsync(PostgresDePrueba.Alumno);

        var destinatario = await Ordenes().DestinatarioAsync(idOrden, PostgresDePrueba.Encargado);

        Assert.NotNull(destinatario);
        Assert.Equal(PostgresDePrueba.Alumno, destinatario.IdUsuario);
    }

    [FactPostgres]
    public async Task Alguien_que_no_es_el_dueno_ni_gestor_de_la_cafeteria_no_puede_pedir_el_destinatario()
    {
        var idOrden = await CrearOrdenAsync(PostgresDePrueba.Alumno);
        var (otroAlumno, _) = await NuevoAlumnoAsync();

        await Assert.ThrowsAsync<ForbiddenException>(() => Ordenes().DestinatarioAsync(idOrden, otroAlumno));
    }

    [FactPostgres]
    public async Task Una_orden_que_no_existe_no_tiene_destinatario()
    {
        Assert.Null(await Ordenes().DestinatarioAsync(Guid.NewGuid(), PostgresDePrueba.Alumno));
    }

    [FactPostgres]
    public async Task La_preferencia_de_aviso_de_pedido_listo_se_respeta_y_por_defecto_esta_activa()
    {
        var (id, _) = await NuevoAlumnoAsync();
        var idOrden = await CrearOrdenAsync(id);
        Assert.True((await Ordenes().DestinatarioAsync(idOrden, id))!.AvisoPedidoListo);       // sin fila de preferencias: activo

        await bd.EjecutarAsync("""
            INSERT INTO preferencia_alumno (id_usuario, aviso_pedido_listo) VALUES (@id, false)
            ON CONFLICT (id_usuario) DO UPDATE SET aviso_pedido_listo = false
            """, new NpgsqlParameter("id", id));

        Assert.False((await Ordenes().DestinatarioAsync(idOrden, id))!.AvisoPedidoListo);
    }

    [FactPostgres]
    public async Task Sin_correo_verificado_o_con_la_cuenta_inactiva_no_hay_a_quien_avisar()
    {
        var (id, _) = await NuevoAlumnoAsync();
        var idOrden = await CrearOrdenAsync(id);
        Assert.NotNull(await Ordenes().DestinatarioAsync(idOrden, PostgresDePrueba.Encargado));

        await bd.EjecutarAsync("UPDATE usuario SET correo_verificado = false WHERE id_usuario = @id", new NpgsqlParameter("id", id));
        Assert.Null(await Ordenes().DestinatarioAsync(idOrden, PostgresDePrueba.Encargado));

        await bd.EjecutarAsync("UPDATE usuario SET correo_verificado = true, activo = false WHERE id_usuario = @id", new NpgsqlParameter("id", id));
        Assert.Null(await Ordenes().DestinatarioAsync(idOrden, PostgresDePrueba.Encargado));
    }
}
