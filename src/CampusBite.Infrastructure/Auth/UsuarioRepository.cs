using CampusBite.Application.Auth;
using CampusBite.Domain.Auth;
using CampusBite.Domain.Common.Exceptions;
using CampusBite.Infrastructure.Persistence;
using static CampusBite.Infrastructure.Persistence.Procedimientos;

namespace CampusBite.Infrastructure.Auth;

// JJ-Sprint2 09/10/2026: implementa IUsuarioRepository con sp_registrar_usuario y sp_obtener_credenciales;
// traduce los errores de PostgreSQL a excepciones del dominio para que Application no conozca Npgsql
// JY-Sprint2 09/10/2026: los sp_* ahora son PROCEDURE: se llaman con CALL, pasando NULL en los parámetros OUT,
// y PostgreSQL devuelve una fila con sus valores. SqlQuery de EF no sirve aquí (envuelve el SQL en un SELECT)
// JY-Sprint3 20/10/2026: la ejecución del CALL y la traducción de errores pasaron a Persistence/Procedimientos.cs
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
            return await _context.EjecutarAsync(
                "CALL sp_registrar_usuario(@nombre, @correo, @hash, @matricula, @telefono, NULL)",
                [Texto("nombre", nombre), Texto("correo", correo), Texto("hash", contrasenaHash),
                 Texto("matricula", matricula), Texto("telefono", telefono)],
                fila => fila.GetGuid(0),
                cancellationToken);
        }
        catch (ConflictException)
        {
            throw new ConflictException("El correo o la matrícula ya están registrados.");
        }
        catch (BusinessRuleException)
        {
            throw new BusinessRuleException("Los datos no cumplen el formato requerido (nombre, correo o teléfono).");
        }
    }

    public Task<CredencialesUsuario?> ObtenerCredencialesAsync(string correo, CancellationToken cancellationToken = default)
    {
        // Si el correo no existe, los cuatro OUT regresan NULL
        return _context.EjecutarAsync(
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

    // JJ-Sprint3 21/10/2026: TEC-12
    public Task RestablecerPasswordAsync(string correo, string contrasenaHash, CancellationToken cancellationToken = default) =>
        _context.EjecutarAsync(
            "CALL sp_restablecer_password(@correo, @hash)",
            [Texto("correo", correo), Texto("hash", contrasenaHash)],
            cancellationToken);
}
