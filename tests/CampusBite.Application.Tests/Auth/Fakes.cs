using CampusBite.Application.Auth;
using CampusBite.Application.Common.Security;
using CampusBite.Domain.Auth;
using CampusBite.Domain.Cafeterias;

namespace CampusBite.Application.Tests.Auth;

// JJ-Sprint2 09/10/2026: dobles de prueba simples; permiten probar AuthService sin base de datos, BCrypt ni JWT reales

internal class FakeUsuarioRepository : IUsuarioRepository
{
    public Dictionary<string, CredencialesUsuario> Usuarios { get; } = new();
    public Exception? ErrorAlRegistrar { get; set; }
    public (string Nombre, string Correo, string Hash, string? Matricula, string? Telefono)? UltimoRegistro { get; private set; }

    public Task<Guid> RegistrarAsync(
        string nombre, string correo, string contrasenaHash, string? matricula, string? telefono,
        CancellationToken cancellationToken = default)
    {
        if (ErrorAlRegistrar is not null)
        {
            throw ErrorAlRegistrar;
        }

        UltimoRegistro = (nombre, correo, contrasenaHash, matricula, telefono);
        var id = Guid.NewGuid();
        Usuarios[correo] = new CredencialesUsuario
        {
            IdUsuario = id, IdRol = RolUsuario.Alumno, ContrasenaHash = contrasenaHash, Activo = true
        };
        return Task.FromResult(id);
    }

    public Task<CredencialesUsuario?> ObtenerCredencialesAsync(string correo, CancellationToken cancellationToken = default)
        => Task.FromResult(Usuarios.GetValueOrDefault(correo));

    // JJ-Sprint3 21/10/2026: TEC-12
    public (string Correo, string Hash)? UltimoRestablecimiento { get; private set; }

    public Task RestablecerPasswordAsync(string correo, string contrasenaHash, CancellationToken cancellationToken = default)
    {
        UltimoRestablecimiento = (correo, contrasenaHash);
        if (Usuarios.TryGetValue(correo, out var credenciales))
        {
            credenciales.ContrasenaHash = contrasenaHash;
        }
        return Task.CompletedTask;
    }

    // JJ-Sprint3 22/10/2026: TEC-11
    public Exception? ErrorAlRegistrarCafeteria { get; set; }
    public (string NombreEncargado, string Correo, string Hash, string? Telefono, string NombreCafeteria,
            string Ubicacion, bool EsDelCampus, string PuntoEntrega, IReadOnlyList<MetodoPago> Metodos)? UltimoRegistroCafeteria
    { get; private set; }

    public Task<Guid> RegistrarCafeteriaAsync(
        string nombreEncargado, string correo, string contrasenaHash, string? telefono,
        string nombreCafeteria, string ubicacion, bool esDelCampus, string puntoEntrega,
        IReadOnlyList<MetodoPago> metodos, CancellationToken cancellationToken = default)
    {
        if (ErrorAlRegistrarCafeteria is not null)
        {
            throw ErrorAlRegistrarCafeteria;
        }

        UltimoRegistroCafeteria = (nombreEncargado, correo, contrasenaHash, telefono, nombreCafeteria,
                                   ubicacion, esDelCampus, puntoEntrega, metodos);
        var id = Guid.NewGuid();
        Usuarios[correo] = new CredencialesUsuario
        {
            IdUsuario = id, IdRol = RolUsuario.AdminCafeteria, ContrasenaHash = contrasenaHash, Activo = true
        };
        return Task.FromResult(id);
    }
}

internal class FakePasswordHasher : IPasswordHasher
{
    public int Verificaciones { get; private set; }

    public string Hash(string password) => $"hash:{password}";

    public bool Verify(string password, string? hash)
    {
        Verificaciones++;
        return hash == $"hash:{password}";
    }
}

internal class FakeTokenService : ITokenService
{
    public (string Token, DateTime ExpiraEn) GenerarToken(Guid idUsuario, string correo, RolUsuario rol)
        => ($"token:{idUsuario}:{rol}", new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc));
}
