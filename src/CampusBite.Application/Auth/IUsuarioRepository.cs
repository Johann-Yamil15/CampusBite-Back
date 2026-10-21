namespace CampusBite.Application.Auth;

// JJ-Sprint2 09/10/2026: acceso a datos de usuarios; Application define el contrato y Infrastructure lo implementa con los sp_*
public interface IUsuarioRepository
{
    // Devuelve el id del alumno creado. Lanza ConflictException si el correo o la matrícula ya existen
    Task<Guid> RegistrarAsync(
        string nombre, string correo, string contrasenaHash, string? matricula, string? telefono,
        CancellationToken cancellationToken = default);

    // null si el correo no está registrado
    Task<CredencialesUsuario?> ObtenerCredencialesAsync(string correo, CancellationToken cancellationToken = default);

    // JJ-Sprint3 21/10/2026: TEC-12. No falla ni avisa si el correo no existe (a propósito, por seguridad)
    Task RestablecerPasswordAsync(string correo, string contrasenaHash, CancellationToken cancellationToken = default);
}
