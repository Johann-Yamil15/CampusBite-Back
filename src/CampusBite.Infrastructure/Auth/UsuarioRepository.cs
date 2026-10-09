using CampusBite.Application.Auth;
using CampusBite.Domain.Common.Exceptions;
using CampusBite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CampusBite.Infrastructure.Auth;

// JJ-Sprint2 09/10/2026: implementa IUsuarioRepository con sp_registrar_usuario y sp_obtener_credenciales;
// traduce los errores de PostgreSQL a excepciones del dominio para que Application no conozca Npgsql
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
            return await _context.Database
                .SqlQuery<Guid>($"""
                    SELECT sp_registrar_usuario({nombre}::varchar, {correo}::varchar, {contrasenaHash}::varchar,
                                                {matricula}::varchar, {telefono}::varchar) AS "Value"
                    """)
                .SingleAsync(cancellationToken);
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
        return _context.Database
            .SqlQuery<CredencialesUsuario>($"""
                SELECT id_usuario, id_rol, contrasena_hash, activo
                  FROM sp_obtener_credenciales({correo}::varchar)
                """)
            .SingleOrDefaultAsync(cancellationToken);
    }
}
