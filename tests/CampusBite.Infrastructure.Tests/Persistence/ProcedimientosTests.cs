using System.Text.Json;
using CampusBite.Application.Cafeterias.Dtos;
using CampusBite.Domain.Cafeterias;
using CampusBite.Domain.Common.Exceptions;
using CampusBite.Infrastructure.Persistence;
using Npgsql;
using NpgsqlTypes;

namespace CampusBite.Infrastructure.Tests.Persistence;

// JY-Sprint3 20/10/2026: qué ve el usuario cuando un procedimiento falla. Regla de seguridad: solo se muestran
// los mensajes que escribimos nosotros (RAISE en un procedimiento); los de PostgreSQL traen nombres de tablas y
// restricciones y se cambian por un texto genérico.
public class ProcedimientosTests
{
    // Así llega un RAISE EXCEPTION ... USING ERRCODE de uno de nuestros procedimientos
    private static PostgresException DeRaise(string sqlState, string mensaje) =>
        new(mensaje, "ERROR", "ERROR", sqlState,
            where: "PL/pgSQL function sp_crear_cafeteria(uuid,character varying) line 12 at RAISE");

    // Así llega un error del propio PostgreSQL dentro de un procedimiento (CHECK, UNIQUE, FK...)
    private static PostgresException DePostgres(string sqlState, string mensaje) =>
        new(mensaje, "ERROR", "ERROR", sqlState,
            where: "SQL statement \"INSERT INTO cafeteria ...\"\nPL/pgSQL function sp_crear_cafeteria(uuid) line 7 at SQL statement",
            tableName: "cafeteria", constraintName: "cafeteria_nombre_check");

    [Theory]
    [InlineData(PostgresErrorCodes.InsufficientPrivilege, typeof(ForbiddenException))]
    [InlineData(PostgresErrorCodes.NoDataFound, typeof(NotFoundException))]
    [InlineData(PostgresErrorCodes.UniqueViolation, typeof(ConflictException))]
    [InlineData(PostgresErrorCodes.ObjectInUse, typeof(ConflictException))]
    [InlineData(PostgresErrorCodes.CheckViolation, typeof(BusinessRuleException))]
    [InlineData(PostgresErrorCodes.RaiseException, typeof(BusinessRuleException))]
    public void Cada_SQLSTATE_propio_se_traduce_a_su_excepcion_con_nuestro_mensaje(string sqlState, Type esperado)
    {
        var error = Procedimientos.Traducir(DeRaise(sqlState, "Mensaje escrito para el usuario"));

        Assert.IsType(esperado, error);
        Assert.Equal("Mensaje escrito para el usuario", error!.Message);
    }

    [Theory]
    [InlineData(PostgresErrorCodes.InsufficientPrivilege, "No tienes permiso para esta acción.")]
    [InlineData(PostgresErrorCodes.NoDataFound, "El recurso no existe.")]
    [InlineData(PostgresErrorCodes.UniqueViolation, "Ya existe un registro con esos datos.")]
    [InlineData(PostgresErrorCodes.ObjectInUse, "El recurso está en uso.")]
    [InlineData(PostgresErrorCodes.CheckViolation, "Los datos no cumplen las reglas.")]
    public void Un_error_interno_de_PostgreSQL_no_muestra_su_mensaje(string sqlState, string generico)
    {
        var error = Procedimientos.Traducir(DePostgres(sqlState,
            "new row for relation \"cafeteria\" violates check constraint \"cafeteria_nombre_check\""));

        Assert.Equal(generico, error!.Message);
        Assert.DoesNotContain("cafeteria", error.Message);
    }

    [Fact]
    public void Un_error_sin_contexto_se_trata_como_interno()
    {
        var error = Procedimientos.Traducir(new PostgresException("detalle interno", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation));

        Assert.Equal("Ya existe un registro con esos datos.", error!.Message);
    }

    // Pasajeros: reintentar sirve. La prueba de carga encontró el primero (pool > límite de conexiones del rol)
    [Theory]
    [InlineData(PostgresErrorCodes.TooManyConnections)]     // 53300
    [InlineData(PostgresErrorCodes.CannotConnectNow)]       // la base está arrancando
    [InlineData(PostgresErrorCodes.LockNotAvailable)]       // lock_timeout de app_backend (3 s)
    [InlineData(PostgresErrorCodes.QueryCanceled)]          // statement_timeout de app_backend (10 s)
    [InlineData(PostgresErrorCodes.DeadlockDetected)]
    [InlineData(PostgresErrorCodes.SerializationFailure)]
    public void Errores_pasajeros_se_traducen_a_servicio_ocupado(string sqlState)
    {
        var error = Procedimientos.Traducir(DePostgres(sqlState, "too many connections for role \"app_backend\""));

        Assert.IsType<ServicioOcupadoException>(error);
        Assert.Equal("El servicio está ocupado. Intenta de nuevo en unos segundos.", error!.Message);
    }

    // Al abrir la conexión, EF entrega el error de PostgreSQL envuelto: hay que encontrarlo para traducirlo
    [Fact]
    public void El_error_de_PostgreSQL_se_encuentra_aunque_EF_lo_envuelva()
    {
        var pg = DePostgres(PostgresErrorCodes.TooManyConnections, "too many connections");

        Assert.Same(pg, Procedimientos.ErrorDePostgres(pg));
        Assert.Same(pg, Procedimientos.ErrorDePostgres(new InvalidOperationException("transient failure", pg)));
        Assert.Null(Procedimientos.ErrorDePostgres(new InvalidOperationException("otra cosa")));
        Assert.Null(Procedimientos.ErrorDePostgres(new TimeoutException("sin PostgreSQL", new Exception("adentro"))));
    }

    [Theory]
    [InlineData(PostgresErrorCodes.UndefinedFunction)]      // falta un procedimiento: error de despliegue
    [InlineData(PostgresErrorCodes.SyntaxError)]
    public void Errores_no_conocidos_no_se_traducen_y_suben_como_500(string sqlState)
    {
        Assert.Null(Procedimientos.Traducir(DeRaise(sqlState, "algo")));
    }

    [Fact]
    public void Json_de_la_BD_se_lee_con_snake_case_y_enums_en_minusculas()
    {
        const string json = """
            {"id_cafeteria":"11111111-1111-1111-1111-111111111111","nombre":"Cafetería","ubicacion":"K",
             "es_del_campus":true,"punto_entrega":"V","activa":false,"metodos_pago":["efectivo","transferencia"],
             "creado_en":"2026-10-20T16:00:00.123+00:00","actualizado_en":"2026-10-20T17:00:00+00:00"}
            """;

        var dto = JsonSerializer.Deserialize<CafeteriaDto>(json, Procedimientos.Json)!;

        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), dto.IdCafeteria);
        Assert.True(dto.EsDelCampus);
        Assert.False(dto.Activa);
        Assert.Equal("V", dto.PuntoEntrega);
        Assert.Equal([MetodoPago.Efectivo, MetodoPago.Transferencia], dto.MetodosPago);
        Assert.Equal(new DateTimeOffset(2026, 10, 20, 17, 0, 0, TimeSpan.Zero), dto.ActualizadoEn);
    }

    [Fact]
    public void Un_enum_numerico_desde_la_BD_se_rechaza()
    {
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<CafeteriaDto>("""{"metodos_pago":[3]}""", Procedimientos.Json));
    }

    [Fact]
    public void Los_enums_se_mandan_a_la_BD_como_texto_en_minusculas()
    {
        var parametro = Procedimientos.Enumeraciones("metodos", [MetodoPago.Tarjeta, MetodoPago.Efectivo]);

        Assert.Equal("metodos", parametro.ParameterName);
        Assert.Equal(NpgsqlDbType.Array | NpgsqlDbType.Text, parametro.NpgsqlDbType);
        Assert.Equal(new[] { "tarjeta", "efectivo" }, (string[])parametro.Value!);
    }

    [Fact]
    public void Parametros_con_tipo_explicito_y_null_como_DBNull()
    {
        var id = Guid.NewGuid();

        Assert.Equal((NpgsqlDbType.Uuid, (object)id), (Procedimientos.Id("id", id).NpgsqlDbType, Procedimientos.Id("id", id).Value!));
        Assert.Equal((NpgsqlDbType.Boolean, (object)true), (Procedimientos.Logico("a", true).NpgsqlDbType, Procedimientos.Logico("a", true).Value!));
        Assert.Equal(NpgsqlDbType.Varchar, Procedimientos.Texto("t", "x").NpgsqlDbType);
        Assert.Equal("x", Procedimientos.Texto("t", "x").Value);
        Assert.Equal(DBNull.Value, Procedimientos.Texto("t", null).Value);
        Assert.Equal((NpgsqlDbType.Integer, (object)7), (Procedimientos.Entero("e", 7).NpgsqlDbType, Procedimientos.Entero("e", 7).Value!));
    }
}
