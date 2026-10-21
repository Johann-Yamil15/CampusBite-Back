using System.Text.Json;
using System.Text.Json.Serialization;
using CampusBite.Domain.Common.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace CampusBite.Infrastructure.Persistence;

// JY-Sprint3 20/10/2026: forma única de llamar a los PROCEDURE de la BD desde cualquier repositorio.
//  - CALL con parámetros (nunca concatenando texto: evita inyección SQL); en los OUT se manda NULL y CALL
//    devuelve una fila con sus valores.
//  - Usa la conexión del DbContext (la abre y la cierra; el pool de Npgsql la reutiliza).
//  - Traduce los errores de PostgreSQL a excepciones de Domain, para que Application no conozca Npgsql.
// Se sacó de UsuarioRepository (Sprint 2) para no repetirlo en cada módulo.
public static class Procedimientos
{
    // JSON que devuelven los procedimientos (claves snake_case, ENUM en minúsculas: "efectivo")
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false) }
    };

    // CALL que devuelve una fila (parámetros OUT); "leer" toma los valores de esa fila
    public static async Task<T> EjecutarAsync<T>(
        this AppDbContext context, string sql, NpgsqlParameter[] parametros,
        Func<NpgsqlDataReader, T> leer, CancellationToken cancellationToken)
    {
        var conexion = (NpgsqlConnection)context.Database.GetDbConnection();
        try
        {
            // Dentro del try: si la base está saturada el error sale al abrir, y también se traduce
            await context.Database.OpenConnectionAsync(cancellationToken);
            await using var comando = new NpgsqlCommand(sql, conexion);
            comando.Parameters.AddRange(parametros);
            await using var fila = await comando.ExecuteReaderAsync(cancellationToken);
            await fila.ReadAsync(cancellationToken);
            return leer(fila);
        }
        catch (Exception ex) when (ErrorDePostgres(ex) is { } pg && Traducir(pg) is { } error)
        {
            throw error;
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    // El error de PostgreSQL de una excepción: EF envuelve los errores al abrir la conexión (p. ej. "too many
    // connections") en InvalidOperationException, así que también se busca adentro
    internal static PostgresException? ErrorDePostgres(Exception ex) =>
        ex as PostgresException ?? ex.InnerException as PostgresException;

    // CALL sin parámetros OUT
    public static Task EjecutarAsync(
        this AppDbContext context, string sql, NpgsqlParameter[] parametros, CancellationToken cancellationToken) =>
        context.EjecutarAsync(sql, parametros, _ => true, cancellationToken);

    // Lee un OUT JSONB y lo convierte al tipo pedido (null si el procedimiento devolvió NULL)
    public static T? LeerJson<T>(this NpgsqlDataReader fila, int columna) =>
        fila.IsDBNull(columna) ? default : JsonSerializer.Deserialize<T>(fila.GetString(columna), Json);

    public static NpgsqlParameter Texto(string nombre, string? valor) =>
        new(nombre, NpgsqlDbType.Varchar) { Value = (object?)valor ?? DBNull.Value };

    public static NpgsqlParameter Id(string nombre, Guid valor) =>
        new(nombre, NpgsqlDbType.Uuid) { Value = valor };

    public static NpgsqlParameter Logico(string nombre, bool valor) =>
        new(nombre, NpgsqlDbType.Boolean) { Value = valor };

    public static NpgsqlParameter Entero(string nombre, int valor) =>
        new(nombre, NpgsqlDbType.Integer) { Value = valor };


    // Arreglo de ENUM: se manda como text[] y en el SQL se convierte con ::metodo_pago[]
    public static NpgsqlParameter Enumeraciones<TEnum>(string nombre, IEnumerable<TEnum> valores) where TEnum : struct, Enum =>
        new(nombre, NpgsqlDbType.Array | NpgsqlDbType.Text)
        {
            Value = valores.Select(v => JsonNamingPolicy.SnakeCaseLower.ConvertName(v.ToString())).ToArray()
        };

    // Los procedimientos lanzan sus errores de negocio con un SQLSTATE y un mensaje en español pensado para el
    // usuario (RAISE EXCEPTION ... USING ERRCODE). Los errores internos de PostgreSQL (CHECK, FK) no se muestran
    // tal cual: su texto trae nombres de tablas y restricciones.
    // Devuelve null si no es un error conocido: entonces sube como 500 y queda en el log.
    internal static DomainException? Traducir(PostgresException ex)
    {
        // "... at RAISE" en el contexto = lo escribió uno de nuestros procedimientos
        var propio = ex.Where?.Contains("at RAISE", StringComparison.Ordinal) == true;
        var mensaje = propio ? ex.MessageText : null;

        return ex.SqlState switch
        {
            PostgresErrorCodes.InsufficientPrivilege => new ForbiddenException(mensaje ?? "No tienes permiso para esta acción."),
            PostgresErrorCodes.NoDataFound => new NotFoundException(mensaje ?? "El recurso no existe."),
            PostgresErrorCodes.UniqueViolation => new ConflictException(mensaje ?? "Ya existe un registro con esos datos."),
            PostgresErrorCodes.ObjectInUse => new ConflictException(mensaje ?? "El recurso está en uso."),
            PostgresErrorCodes.CheckViolation => new BusinessRuleException(mensaje ?? "Los datos no cumplen las reglas."),
            PostgresErrorCodes.RaiseException => new BusinessRuleException(ex.MessageText),
            // JY-Sprint3 20/10/2026: errores pasajeros (los encontró la prueba de carga): reintentar sirve -> 503
            PostgresErrorCodes.TooManyConnections or PostgresErrorCodes.CannotConnectNow or
            PostgresErrorCodes.LockNotAvailable or PostgresErrorCodes.QueryCanceled or
            PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure
                => new ServicioOcupadoException("El servicio está ocupado. Intenta de nuevo en unos segundos."),
            _ => null
        };
    }
}
