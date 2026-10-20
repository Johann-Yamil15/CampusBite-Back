using CampusBite.Application.Auth;
using CampusBite.Domain.Auth;
using CampusBite.Domain.Common.Exceptions;
using CampusBite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace CampusBite.Infrastructure.Auth;

// JJ-Sprint2 09/10/2026: implementa IUsuarioRepository con sp_registrar_usuario y sp_obtener_credenciales;
// traduce los errores de PostgreSQL a excepciones del dominio para que Application no conozca Npgsql
// JY-Sprint2 09/10/2026: los sp_* ahora son PROCEDURE: se llaman con CALL, pasando NULL en los parámetros OUT,
// y PostgreSQL devuelve una fila con sus valores. SqlQuery de EF no sirve aquí (envuelve el SQL en un SELECT)
public class UsuarioRepository : IUsuarioRepository
{
    private readonly AppDbContext _context;

    public UsuarioRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> RegistrarAsync(
        string nombre, string correo, string contrasenaHash, string? matricula, string? telefono,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await EjecutarAsync(
                "CALL sp_registrar_usuario(@nombre, @correo, @hash, @matricula, @telefono, NULL)",
                [Texto("nombre", nombre), Texto("correo", correo), Texto("hash", contrasenaHash),
                 Texto("matricula", matricula), Texto("telefono", telefono)],
                fila => fila.GetGuid(0),
                cancellationToken);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new ConflictException("El correo o la matrícula ya están registrados.");
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.CheckViolation)
        {
            throw new BusinessRuleException("Los datos no cumplen el formato requerido (nombre, correo o teléfono).");
        }
    }

    public Task<CredencialesUsuario?> ObtenerCredencialesAsync(string correo, CancellationToken cancellationToken = default)
    {
        // Si el correo no existe, los cuatro OUT regresan NULL
        return EjecutarAsync(
            "CALL sp_obtener_credenciales(@correo, NULL, NULL, NULL, NULL)",
            [Texto("correo", correo)],
            fila => fila.IsDBNull(0)
                ? null
                : new CredencialesUsuario
                {
                    IdUsuario = fila.GetGuid(0),
                    IdRol = (RolUsuario)fila.GetInt16(1),
                    ContrasenaHash = fila.GetString(2),
                    Activo = fila.GetBoolean(3)
                },
            cancellationToken);
    }

    private static NpgsqlParameter Texto(string nombre, string? valor) =>
        new(nombre, NpgsqlDbType.Varchar) { Value = (object?)valor ?? DBNull.Value };

    // Ejecuta un CALL y lee la única fila con los parámetros OUT, usando la conexión del DbContext
    private async Task<T> EjecutarAsync<T>(
        string sql, NpgsqlParameter[] parametros, Func<NpgsqlDataReader, T> leer, CancellationToken cancellationToken)
    {
        var conexion = (NpgsqlConnection)_context.Database.GetDbConnection();
        await _context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var comando = new NpgsqlCommand(sql, conexion);
            comando.Parameters.AddRange(parametros);
            await using var fila = await comando.ExecuteReaderAsync(cancellationToken);
            await fila.ReadAsync(cancellationToken);
            return leer(fila);
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }
    }
}
