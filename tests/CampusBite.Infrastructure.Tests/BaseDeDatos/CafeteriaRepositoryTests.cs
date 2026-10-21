using CampusBite.Application.Cafeterias;
using CampusBite.Domain.Cafeterias;
using CampusBite.Domain.Common.Exceptions;
using CampusBite.Infrastructure.Auth;
using CampusBite.Infrastructure.Cafeterias;
using CampusBite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using static CampusBite.Infrastructure.Tests.BaseDeDatos.ConBaseDeDatos;

namespace CampusBite.Infrastructure.Tests.BaseDeDatos;

// JY-Sprint3 20/10/2026: TEC-08. CafeteriaRepository contra la BD real: CALL, lectura del JSON, arreglo de
// ENUM y traducción de los errores que lanzan los procedimientos.
public class CafeteriaRepositoryTests
{
    private static DatosCafeteria Datos(string nombre, params MetodoPago[] metodos) =>
        new(nombre, "Edificio K", true, "Ventanilla 2", metodos.Length > 0 ? metodos : [MetodoPago.Efectivo]);

    [FactConBaseDeDatos]
    public async Task Ciclo_completo_crear_leer_editar_desactivar_y_reactivar()
    {
        await using var contexto = Contexto();
        var repositorio = new CafeteriaRepository(contexto);
        var nombre = NombreUnico("Repo");

        var id = await repositorio.CrearAsync(AdminSistema, Datos(nombre, MetodoPago.Tarjeta, MetodoPago.Efectivo));
        var creada = await repositorio.ObtenerAsync(id, Alumno);
        Assert.NotNull(creada);
        Assert.Equal(nombre, creada!.Nombre);
        Assert.True(creada.Activa);
        Assert.Equal([MetodoPago.Tarjeta, MetodoPago.Efectivo], creada.MetodosPago);
        Assert.True(creada.CreadoEn > DateTimeOffset.UtcNow.AddMinutes(-5));

        await repositorio.ActualizarAsync(id, AdminSistema, Datos(nombre + "!", MetodoPago.Transferencia));
        var editada = await repositorio.ObtenerAsync(id, Alumno);
        Assert.Equal(nombre + "!", editada!.Nombre);
        Assert.Equal([MetodoPago.Transferencia], editada.MetodosPago);

        await repositorio.CambiarActivaAsync(id, false, AdminSistema);
        Assert.Null(await repositorio.ObtenerAsync(id, Alumno));                  // oculta para un alumno
        Assert.False((await repositorio.ObtenerAsync(id, AdminSistema))!.Activa);   // visible para el admin
        Assert.DoesNotContain((await repositorio.ListarAsync(Alumno, false, nombre, 100, 0)).Cafeterias, c => c.IdCafeteria == id);
        Assert.Contains((await repositorio.ListarAsync(AdminSistema, true, nombre, 100, 0)).Cafeterias, c => c.IdCafeteria == id);

        await repositorio.CambiarActivaAsync(id, true, AdminSistema);
        Assert.Contains((await repositorio.ListarAsync(Alumno, false, nombre, 100, 0)).Cafeterias, c => c.IdCafeteria == id);
    }

    // Cada CALL abre la conexión y la devuelve al pool, también cuando el procedimiento falla
    [FactConBaseDeDatos]
    public async Task La_conexion_se_cierra_despues_de_cada_llamada_aunque_falle()
    {
        await using var contexto = Contexto();
        var repositorio = new CafeteriaRepository(contexto);
        var conexion = contexto.Database.GetDbConnection();

        await repositorio.ListarAsync(Alumno, false, null, 20, 0);
        Assert.Equal(System.Data.ConnectionState.Closed, conexion.State);

        await Assert.ThrowsAsync<ForbiddenException>(() => repositorio.ListarAsync(Alumno, true, null, 20, 0));
        Assert.Equal(System.Data.ConnectionState.Closed, conexion.State);
    }

    [FactConBaseDeDatos]
    public async Task Listar_pagina_en_orden_con_total_y_busqueda()
    {
        await using var contexto = Contexto();
        var repositorio = new CafeteriaRepository(contexto);
        var prefijo = NombreUnico("Pag");
        foreach (var i in Enumerable.Range(1, 7))
            await repositorio.CrearAsync(AdminSistema, Datos($"{prefijo} {i}"));

        var (pagina1, total) = await repositorio.ListarAsync(Alumno, false, prefijo.ToUpperInvariant(), 3, 0);
        var (pagina3, _) = await repositorio.ListarAsync(Alumno, false, prefijo, 3, 6);

        Assert.Equal(7, total);
        Assert.Equal([$"{prefijo} 1", $"{prefijo} 2", $"{prefijo} 3"], pagina1.Select(c => c.Nombre));
        Assert.Equal([$"{prefijo} 7"], pagina3.Select(c => c.Nombre));
    }

    [FactConBaseDeDatos]
    public async Task Obtener_una_que_no_existe_devuelve_null()
    {
        await using var contexto = Contexto();

        Assert.Null(await new CafeteriaRepository(contexto).ObtenerAsync(Guid.NewGuid(), AdminSistema));
    }

    [FactConBaseDeDatos]
    public async Task Los_errores_de_los_procedimientos_llegan_como_excepciones_de_Domain_con_su_mensaje()
    {
        await using var contexto = Contexto();
        var repositorio = new CafeteriaRepository(contexto);
        var nombre = NombreUnico("Errores");
        var id = await repositorio.CrearAsync(AdminSistema, Datos(nombre));

        var sinPermiso = await Assert.ThrowsAsync<ForbiddenException>(() => repositorio.CrearAsync(Alumno, Datos(NombreUnico("X"))));
        Assert.Equal("Sin permiso para crear cafeterías", sinPermiso.Message);

        var repetido = await Assert.ThrowsAsync<ConflictException>(() => repositorio.CrearAsync(AdminSistema, Datos(nombre.ToUpperInvariant())));
        Assert.Contains("Ya existe una cafetería con el nombre", repetido.Message);

        var noExiste = await Assert.ThrowsAsync<NotFoundException>(() => repositorio.ActualizarAsync(Guid.NewGuid(), AdminSistema, Datos(NombreUnico("Y"))));
        Assert.Equal("La cafetería no existe", noExiste.Message);

        var otroEncargado = await Assert.ThrowsAsync<ForbiddenException>(() => repositorio.ActualizarAsync(id, Encargado, Datos(nombre)));
        Assert.Equal("Sin permiso para editar esta cafetería", otroEncargado.Message);

        var invalido = await Assert.ThrowsAsync<BusinessRuleException>(() => repositorio.CrearAsync(AdminSistema, Datos(NombreUnico("Z")) with { MetodosPago = [] }));
        Assert.Equal("La cafetería debe aceptar al menos un método de pago", invalido.Message);
    }

    [FactConBaseDeDatos]
    public async Task No_se_desactiva_con_ordenes_en_curso()
    {
        await using var contexto = Contexto();
        var repositorio = new CafeteriaRepository(contexto);

        // La cafetería de prueba tiene órdenes en curso si alguna prueba de órdenes ya corrió; si no, se crea una
        await contexto.EjecutarAsync(
            """
            CALL sp_crear_orden(@alumno, @cafe,
                 (date_trunc('day', now() AT TIME ZONE 'America/Mexico_City') + interval '1 day 13 hours') AT TIME ZONE 'America/Mexico_City',
                 'efectivo', '[{"id_producto":"00000000-0000-0000-0000-0000000000e1","cantidad":1}]', NULL, NULL)
            """,
            [Procedimientos.Id("alumno", Alumno), Procedimientos.Id("cafe", CafeteriaDePrueba)], _ => true, default);

        var error = await Assert.ThrowsAsync<ConflictException>(() => repositorio.CambiarActivaAsync(CafeteriaDePrueba, false, AdminSistema));
        Assert.Contains("orden(es) en curso", error.Message);
    }

    [FactConBaseDeDatos]
    public async Task UsuarioRepository_registra_y_lee_credenciales_con_CALL()
    {
        await using var contexto = Contexto();
        var usuarios = new UsuarioRepository(contexto);
        var correo = $"repo{Guid.NewGuid():N}@ejemplo.test";

        var id = await usuarios.RegistrarAsync("Alumno Repo", correo, "HASH", null, null);
        var credenciales = await usuarios.ObtenerCredencialesAsync(correo);

        Assert.Equal(id, credenciales!.IdUsuario);
        Assert.Equal("HASH", credenciales.ContrasenaHash);
        Assert.True(credenciales.Activo);
        Assert.Null(await usuarios.ObtenerCredencialesAsync($"no{correo}"));
        await Assert.ThrowsAsync<ConflictException>(() => usuarios.RegistrarAsync("Otro", correo.ToUpperInvariant(), "H", null, null));
    }
}
