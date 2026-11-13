using CampusBite.Application.Cafeterias;
using CampusBite.Domain.Cafeterias;
using CampusBite.Domain.Common.Exceptions;
using CampusBite.Infrastructure.Cafeterias;
using Npgsql;

namespace CampusBite.Api.Tests.Postgres;

// CafeteriaRepository contra los procedimientos reales (sp_listar_cafeterias, sp_obtener_cafeteria, sp_crear_cafeteria,
// sp_actualizar_cafeteria, sp_cambiar_activa_cafeteria). Cada prueba crea sus propias cafeterías con nombre único y
// solo LEE la cafetería semilla, para no depender del orden de ejecución.
[Collection(ColeccionPostgres.Nombre)]
public class CafeteriaRepositoryTests(PostgresDePrueba bd)
{
    private CafeteriaRepository Repo() => new(bd.Contexto());

    private static string Unico(string prefijo) => $"{prefijo} {Guid.NewGuid().ToString("N")[..8]}";

    private static DatosCafeteria Datos(string nombre, bool campus = false, params MetodoPago[] metodos) =>
        new(nombre, "Edificio B, planta alta", campus, "Ventanilla 2", metodos.Length == 0 ? [MetodoPago.Efectivo] : metodos);

    [FactPostgres]
    public async Task Obtener_mapea_todos_los_campos_de_la_cafeteria_semilla()
    {
        var c = await Repo().ObtenerAsync(PostgresDePrueba.Cafeteria, PostgresDePrueba.Alumno);

        Assert.NotNull(c);
        Assert.Equal(PostgresDePrueba.Cafeteria, c.IdCafeteria);
        Assert.Equal("Cafetería Central (prueba)", c.Nombre);
        Assert.Equal("Edificio principal, planta baja", c.Ubicacion);
        Assert.True(c.EsDelCampus);
        Assert.Equal("Puerta 1", c.PuntoEntrega);
        Assert.True(c.Activa);
        Assert.Equal([MetodoPago.Tarjeta, MetodoPago.Transferencia, MetodoPago.Efectivo], c.MetodosPago.Order());
        Assert.NotEqual(default, c.CreadoEn);
        Assert.True(c.ActualizadoEn >= c.CreadoEn);
    }

    [FactPostgres]
    public async Task Obtener_una_que_no_existe_devuelve_null()
    {
        Assert.Null(await Repo().ObtenerAsync(Guid.NewGuid(), PostgresDePrueba.Admin));
    }

    [FactPostgres]
    public async Task Crear_guarda_exactamente_los_datos_enviados_y_devuelve_el_id_real()
    {
        var nombre = Unico("Cafetería Norte");

        var id = await Repo().CrearAsync(PostgresDePrueba.Admin,
            Datos(nombre, campus: false, MetodoPago.Tarjeta, MetodoPago.Transferencia));

        var c = await Repo().ObtenerAsync(id, PostgresDePrueba.Admin);
        Assert.NotNull(c);
        Assert.Equal(id, c.IdCafeteria);
        Assert.Equal(nombre, c.Nombre);
        Assert.Equal("Edificio B, planta alta", c.Ubicacion);
        Assert.False(c.EsDelCampus);
        Assert.Equal("Ventanilla 2", c.PuntoEntrega);
        Assert.Equal([MetodoPago.Tarjeta, MetodoPago.Transferencia], c.MetodosPago.Order());
        Assert.Equal(1L, await bd.EscalarAsync<long>(
            "SELECT count(*) FROM cafeteria WHERE id_cafeteria = @id AND nombre = @n AND NOT es_del_campus",
            new NpgsqlParameter("id", id), new NpgsqlParameter("n", nombre)));
    }

    [FactPostgres]
    public async Task Crear_con_un_nombre_repetido_da_Conflict_con_el_mensaje_del_procedimiento()
    {
        var nombre = Unico("Repetida");
        await Repo().CrearAsync(PostgresDePrueba.Admin, Datos(nombre));

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            Repo().CrearAsync(PostgresDePrueba.Admin, Datos("  " + nombre.ToUpperInvariant() + " ")));

        Assert.Contains("Ya existe una cafetería", ex.Message);
    }

    [FactPostgres]
    public async Task Crear_sin_ser_admin_del_sistema_da_Forbidden_y_no_crea_nada()
    {
        var nombre = Unico("Sin permiso");

        await Assert.ThrowsAsync<ForbiddenException>(() => Repo().CrearAsync(PostgresDePrueba.Encargado, Datos(nombre)));

        Assert.Equal(0L, await bd.EscalarAsync<long>("SELECT count(*) FROM cafeteria WHERE nombre = @n", new NpgsqlParameter("n", nombre)));
    }

    [FactPostgres]
    public async Task Crear_dos_con_el_mismo_nombre_a_la_vez_solo_crea_una()
    {
        var nombre = Unico("Concurrente");

        var intentos = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            try { await Repo().CrearAsync(PostgresDePrueba.Admin, Datos(nombre)); return "ok"; }
            catch (ConflictException) { return "conflicto"; }
        }));

        Assert.Equal(["conflicto", "ok"], intentos.Order());
        Assert.Equal(1L, await bd.EscalarAsync<long>("SELECT count(*) FROM cafeteria WHERE lower(nombre) = lower(@n)", new NpgsqlParameter("n", nombre)));
    }

    [FactPostgres]
    public async Task Actualizar_cambia_todos_los_campos_y_los_metodos_de_pago()
    {
        var id = await Repo().CrearAsync(PostgresDePrueba.Admin, Datos(Unico("Para editar"), true, MetodoPago.Efectivo));
        var nuevo = Unico("Editada");

        await Repo().ActualizarAsync(id, PostgresDePrueba.Admin,
            new DatosCafeteria(nuevo, "Edificio C", false, "Mostrador", [MetodoPago.Transferencia]));

        var c = await Repo().ObtenerAsync(id, PostgresDePrueba.Admin);
        Assert.NotNull(c);
        Assert.Equal((nuevo, "Edificio C", false, "Mostrador"), (c.Nombre, c.Ubicacion, c.EsDelCampus, c.PuntoEntrega));
        Assert.Equal([MetodoPago.Transferencia], c.MetodosPago);
    }

    [FactPostgres]
    public async Task Actualizar_una_cafeteria_ajena_da_Forbidden_e_inexistente_NotFound()
    {
        var ajena = await Repo().CrearAsync(PostgresDePrueba.Admin, Datos(Unico("Ajena")));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Repo().ActualizarAsync(ajena, PostgresDePrueba.Encargado, Datos(Unico("Intento"))));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Repo().ActualizarAsync(Guid.NewGuid(), PostgresDePrueba.Admin, Datos(Unico("Fantasma"))));
    }

    [FactPostgres]
    public async Task Desactivar_y_reactivar_se_refleja_y_repetir_no_es_error()
    {
        var id = await Repo().CrearAsync(PostgresDePrueba.Admin, Datos(Unico("Temporal")));

        await Repo().CambiarActivaAsync(id, false, PostgresDePrueba.Admin);
        await Repo().CambiarActivaAsync(id, false, PostgresDePrueba.Admin);          // idempotente
        Assert.False((await Repo().ObtenerAsync(id, PostgresDePrueba.Admin))!.Activa);
        Assert.Null(await Repo().ObtenerAsync(id, PostgresDePrueba.Alumno));          // inactiva: un alumno no la ve

        await Repo().CambiarActivaAsync(id, true, PostgresDePrueba.Admin);
        Assert.True((await Repo().ObtenerAsync(id, PostgresDePrueba.Alumno))!.Activa);
    }

    [FactPostgres]
    public async Task Cambiar_activa_sin_permiso_o_inexistente_se_traduce()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            Repo().CambiarActivaAsync(PostgresDePrueba.Cafeteria, false, PostgresDePrueba.Alumno));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Repo().CambiarActivaAsync(Guid.NewGuid(), false, PostgresDePrueba.Admin));
    }

    [FactPostgres]
    public async Task Listar_filtra_pagina_y_devuelve_el_total_real()
    {
        var marca = Guid.NewGuid().ToString("N")[..8];
        for (var i = 1; i <= 3; i++)
        {
            await Repo().CrearAsync(PostgresDePrueba.Admin, Datos($"Lista {marca} {i}"));
        }

        var (total, pagina) = await Repo().ListarAsync(PostgresDePrueba.Admin, true, marca, 2, 0);
        var (_, resto) = await Repo().ListarAsync(PostgresDePrueba.Admin, true, marca, 2, 2);

        Assert.Equal(3, total);
        Assert.Equal(2, pagina.Count);
        Assert.Single(resto);
        Assert.All(pagina.Concat(resto), c => Assert.Contains(marca, c.Nombre));
        Assert.Equal(3, pagina.Concat(resto).Select(c => c.IdCafeteria).Distinct().Count());
    }

    [FactPostgres]
    public async Task Listar_sin_coincidencias_devuelve_total_cero_y_lista_vacia()
    {
        var (total, items) = await Repo().ListarAsync(PostgresDePrueba.Admin, false, "no-existe-" + Guid.NewGuid(), 20, 0);

        Assert.Equal(0, total);
        Assert.Empty(items);
    }

    [FactPostgres]
    public async Task Listar_sin_inactivas_oculta_las_desactivadas()
    {
        var marca = Guid.NewGuid().ToString("N")[..8];
        var id = await Repo().CrearAsync(PostgresDePrueba.Admin, Datos($"Oculta {marca}"));
        await Repo().CambiarActivaAsync(id, false, PostgresDePrueba.Admin);

        var (soloActivas, _) = await Repo().ListarAsync(PostgresDePrueba.Admin, false, marca, 20, 0);
        var (todas, items) = await Repo().ListarAsync(PostgresDePrueba.Admin, true, marca, 20, 0);

        Assert.Equal(0, soloActivas);
        Assert.Equal(1, todas);
        Assert.False(Assert.Single(items).Activa);
    }
}
