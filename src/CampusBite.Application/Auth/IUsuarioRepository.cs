using CampusBite.Domain.Cafeterias;

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

    // JJ-Sprint3 22/10/2026: TEC-11. Crea al encargado (admin_cafeteria) y su cafetería en una transacción y
    // devuelve el id del encargado. Lanza ConflictException si el correo o el nombre de la cafetería ya existen
    Task<Guid> RegistrarCafeteriaAsync(
        string nombreEncargado, string correo, string contrasenaHash, string? telefono,
        string nombreCafeteria, string ubicacion, bool esDelCampus, string puntoEntrega,
        IReadOnlyList<MetodoPago> metodos, CancellationToken cancellationToken = default);
}
