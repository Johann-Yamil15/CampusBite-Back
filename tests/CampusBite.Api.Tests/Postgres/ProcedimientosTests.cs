using System.Data;
using CampusBite.Domain.Cafeterias;
using CampusBite.Domain.Common.Exceptions;
using CampusBite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using static CampusBite.Infrastructure.Persistence.Procedimientos;

namespace CampusBite.Api.Tests.Postgres;

// Persistence/Procedimientos.cs contra PostgreSQL real: ejecución, lectura de la fila OUT, parámetros, cierre de la
// conexión y la traducción de SQLSTATE a excepciones de Domain (Traducir), que decide todos los códigos HTTP de error.
//  - "Propio" = el error lo lanzó un RAISE de nuestros procedimientos: se muestra su mensaje.
//  - "Interno" = lo lanzó PostgreSQL (CHECK, UNIQUE, permisos...): se muestra un mensaje genérico, nunca el texto
//    interno (trae nombres de tablas y restricciones).
[Collection(ColeccionPostgres.Nombre)]
public class ProcedimientosTests(PostgresDePrueba bd)
{
    private const string MensajePropio = "Mensaje propio del procedimiento";

    private static string RaisePropio(string sqlState) =>
        $"DO $$ BEGIN RAISE EXCEPTION '{MensajePropio}' USING ERRCODE = '{sqlState}'; END $$";

    // ---- Ejecución correcta y lectura -------------------------------------------------------------------------

    [FactPostgres]
    public async Task Lee_la_fila_que_devuelve_el_comando_con_sus_parametros()
    {
        await using var contexto = bd.Contexto();

        var fila = await contexto.EjecutarAsync(
            "SELECT @t::text, @id::uuid, @n::numeric, @e::int, @l::bool",
            [Texto("t", "hola"), Id("id", PostgresDePrueba.Cafeteria), Numero("n", 12.50m), Entero("e", 7), Logico("l", true)],
            r => (r.GetString(0), r.GetGuid(1), r.GetDecimal(2), r.GetInt32(3), r.GetBoolean(4)),
            CancellationToken.None);

        Assert.Equal(("hola", PostgresDePrueba.Cafeteria, 12.50m, 7, true), fila);
        Assert.Equal(ConnectionState.Closed, contexto.Database.GetDbConnection().State);   // la cierra al terminar
    }

    [FactPostgres]
    public async Task Los_parametros_opcionales_viajan_como_NULL()
    {
        await using var contexto = bd.Contexto();

        var nulos = await contexto.EjecutarAsync(
            "SELECT @t IS NULL, @id IS NULL",
            [Texto("t", null), IdOpcional("id", null)],
            r => (r.GetBoolean(0), r.GetBoolean(1)),
            CancellationToken.None);
        var conValor = await contexto.EjecutarAsync(
            "SELECT @id::uuid", [IdOpcional("id", PostgresDePrueba.Desayunos)], r => r.GetGuid(0), CancellationToken.None);

        Assert.Equal((true, true), nulos);
        Assert.Equal(PostgresDePrueba.Desayunos, conValor);
    }

    [FactPostgres]
    public async Task Las_enumeraciones_se_mandan_en_snake_case_y_convierten_al_ENUM_de_la_BD()
    {
        await using var contexto = bd.Contexto();

        var texto = await contexto.EjecutarAsync(
            "SELECT (@m::metodo_pago[])::text",
            [Enumeraciones("m", new[] { MetodoPago.Tarjeta, MetodoPago.Transferencia, MetodoPago.Efectivo })],
            r => r.GetString(0),
            CancellationToken.None);

        Assert.Equal("{tarjeta,transferencia,efectivo}", texto);
    }

    [FactPostgres]
    public async Task LeerJson_convierte_snake_case_y_devuelve_default_si_es_NULL()
    {
        await using var contexto = bd.Contexto();

        var (objeto, nulo) = await contexto.EjecutarAsync(
            "SELECT '{\"id_categoria\":\"00000000-0000-0000-0000-0000000000d1\",\"nombre\":\"Desayunos\"}'::jsonb, NULL::jsonb",
            [],
            r => (r.LeerJson<CampusBite.Application.Productos.Dtos.CategoriaDto>(0), r.LeerJson<CampusBite.Application.Productos.Dtos.CategoriaDto>(1)),
            CancellationToken.None);

        Assert.Equal(new CampusBite.Application.Productos.Dtos.CategoriaDto(PostgresDePrueba.Desayunos, "Desayunos"), objeto);
        Assert.Null(nulo);
    }

    [FactPostgres]
    public async Task El_JSON_lee_enums_en_minusculas_y_rechaza_numeros()
    {
        await using var contexto = bd.Contexto();

        var metodos = await contexto.EjecutarAsync(
            "SELECT '[\"efectivo\",\"tarjeta\"]'::jsonb", [], r => r.LeerJson<List<MetodoPago>>(0), CancellationToken.None);
        Assert.Equal([MetodoPago.Efectivo, MetodoPago.Tarjeta], metodos);

        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => contexto.EjecutarAsync(
            "SELECT '[3]'::jsonb", [], r => r.LeerJson<List<MetodoPago>>(0), CancellationToken.None));
    }

    [FactPostgres]
    public async Task La_version_sin_OUT_ejecuta_el_comando()
    {
        var marca = "procedimientos_" + Guid.NewGuid().ToString("N")[..8];
        await using var contexto = bd.Contexto();

        await contexto.EjecutarAsync(
            "CALL sp_crear_categoria(@c, @a, @n, NULL)",
            [Id("c", PostgresDePrueba.Cafeteria), Id("a", PostgresDePrueba.Encargado), Texto("n", marca)],
            CancellationToken.None);

        Assert.Equal(1L, await bd.EscalarAsync<long>("SELECT count(*) FROM categoria WHERE nombre = @n", new NpgsqlParameter("n", marca)));
    }

    // ---- Traducción de errores de PostgreSQL ------------------------------------------------------------------

    public static TheoryData<string, Type> Conocidos => new()
    {
        { "42501", typeof(ForbiddenException) },          // insufficient_privilege
        { "P0002", typeof(NotFoundException) },           // no_data_found
        { "23505", typeof(ConflictException) },           // unique_violation
        { "55006", typeof(ConflictException) },           // object_in_use
        { "23514", typeof(BusinessRuleException) },       // check_violation
        { "P0001", typeof(BusinessRuleException) }        // raise_exception
    };

    [TheoryPostgres]
    [MemberData(nameof(Conocidos))]
    public async Task Un_RAISE_propio_se_traduce_con_su_mensaje(string sqlState, Type esperado)
    {
        await using var contexto = bd.Contexto();

        var ex = await Assert.ThrowsAnyAsync<DomainException>(() =>
            contexto.EjecutarAsync(RaisePropio(sqlState), [], CancellationToken.None));

        Assert.IsType(esperado, ex);
        Assert.Equal(MensajePropio, ex.Message);
        Assert.Equal(ConnectionState.Closed, contexto.Database.GetDbConnection().State);
    }

    // Errores que lanza PostgreSQL mismo (sin RAISE): mensaje genérico, nunca el texto interno
    public static TheoryData<string, Type, string, string> Internos => new()
    {
        {
            "42501", typeof(ForbiddenException), "No tienes permiso para esta acción.",
            "DO $$ BEGIN EXECUTE 'SET LOCAL ROLE app_backend'; PERFORM 1 FROM usuario; END $$"
        },
        {
            "P0002", typeof(NotFoundException), "El recurso no existe.",
            "DO $$ DECLARE x int; BEGIN SELECT 1 INTO STRICT x WHERE false; END $$"
        },
        {
            "23505", typeof(ConflictException), "Ya existe un registro con esos datos.",
            "DO $$ BEGIN CREATE TEMP TABLE t_dup (x int PRIMARY KEY) ON COMMIT DROP; INSERT INTO t_dup VALUES (1), (1); END $$"
        },
        {
            "55006", typeof(ConflictException), "El recurso está en uso.",
            "DO $$ DECLARE c refcursor; BEGIN CREATE TEMP TABLE t_uso (x int) ON COMMIT DROP; " +
            "OPEN c FOR SELECT * FROM t_uso; ALTER TABLE t_uso ADD COLUMN y int; END $$"
        },
        {
            "23514", typeof(BusinessRuleException), "Los datos no cumplen las reglas.",
            "DO $$ BEGIN CREATE TEMP TABLE t_ck (x int CHECK (x > 0)) ON COMMIT DROP; INSERT INTO t_ck VALUES (-1); END $$"
        }
    };

    [TheoryPostgres]
    [MemberData(nameof(Internos))]
    public async Task Un_error_interno_se_traduce_con_mensaje_generico(string sqlState, Type esperado, string mensaje, string sql)
    {
        await using var contexto = bd.Contexto();

        var ex = await Assert.ThrowsAnyAsync<DomainException>(() => contexto.EjecutarAsync(sql, [], CancellationToken.None));

        Assert.IsType(esperado, ex);
        Assert.Equal(mensaje, ex.Message);
        Assert.DoesNotContain("t_", ex.Message);                 // nunca el nombre de la tabla temporal
        Assert.DoesNotContain(sqlState, ex.Message);
    }

    [FactPostgres]
    public async Task Un_CHECK_real_de_una_tabla_no_filtra_nombres_de_restricciones()
    {
        await using var contexto = bd.Contexto();

        // ck de producto: precio > 0. PostgreSQL diría el nombre de la tabla y de la restricción
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => contexto.EjecutarAsync(
            "UPDATE producto SET precio = -1 WHERE id_producto = @p", [Id("p", PostgresDePrueba.Cafe)], CancellationToken.None));

        Assert.Equal("Los datos no cumplen las reglas.", ex.Message);
        Assert.DoesNotContain("producto", ex.Message);
    }

    [TheoryPostgres]
    [InlineData("SELECT 1/0", "22012")]                                                 // interno desconocido
    [InlineData("DO $$ BEGIN RAISE EXCEPTION 'otro' USING ERRCODE = '22023'; END $$", "22023")]   // RAISE con código no mapeado
    public async Task Un_SQLSTATE_no_mapeado_sube_tal_cual_como_PostgresException(string sql, string sqlState)
    {
        await using var contexto = bd.Contexto();

        var ex = await Assert.ThrowsAsync<PostgresException>(() => contexto.EjecutarAsync(sql, [], CancellationToken.None));

        Assert.Equal(sqlState, ex.SqlState);
        Assert.Equal(ConnectionState.Closed, contexto.Database.GetDbConnection().State);
    }

    // ---- Errores que no son de PostgreSQL ---------------------------------------------------------------------

    [FactPostgres]
    public async Task Un_error_al_leer_la_fila_sube_sin_traducir_y_cierra_la_conexion()
    {
        await using var contexto = bd.Contexto();

        await Assert.ThrowsAsync<InvalidCastException>(() =>
            contexto.EjecutarAsync("SELECT 'no es uuid'::text", [], r => r.GetGuid(0), CancellationToken.None));

        Assert.Equal(ConnectionState.Closed, contexto.Database.GetDbConnection().State);
    }

    [FactPostgres]
    public async Task Sin_servidor_el_error_de_conexion_no_se_disfraza_de_error_de_negocio()
    {
        var sinServidor = new NpgsqlConnectionStringBuilder(bd.Conexion) { Host = "127.0.0.1", Port = 1, Timeout = 2 }.ConnectionString;
        await using var contexto = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(sinServidor).Options);

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => contexto.EjecutarAsync("SELECT 1", [], CancellationToken.None));

        // EF Core envuelve la falla transitoria de Npgsql en InvalidOperationException: no es DomainException, así que
        // ExceptionMiddleware la responde como 500 y la registra (no como 400/404/409)
        Assert.IsNotAssignableFrom<DomainException>(ex);
        var cadena = new List<Exception>();
        for (Exception? e = ex; e is not null; e = e.InnerException) cadena.Add(e);
        Assert.Contains(cadena, e => e is NpgsqlException);
    }

    [FactPostgres]
    public async Task Cancelar_antes_de_empezar_no_ejecuta_nada()
    {
        await using var contexto = bd.Contexto();
        using var cancelado = new CancellationTokenSource();
        await cancelado.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            contexto.EjecutarAsync("SELECT 1", [], cancelado.Token));
    }

    [FactPostgres]
    public async Task Cancelar_durante_la_consulta_la_interrumpe_y_cierra_la_conexion()
    {
        await using var contexto = bd.Contexto();
        using var cancelar = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var reloj = System.Diagnostics.Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            contexto.EjecutarAsync("SELECT pg_sleep(10)", [], cancelar.Token));

        Assert.True(reloj.Elapsed < TimeSpan.FromSeconds(8), $"La cancelación tardó {reloj.Elapsed}");
        Assert.Equal(ConnectionState.Closed, contexto.Database.GetDbConnection().State);
    }
}
