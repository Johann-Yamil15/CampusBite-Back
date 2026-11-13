using CampusBite.Application.Auth;
using CampusBite.Application.Auth.Dtos;
using CampusBite.Application.Tests.Productos;
using CampusBite.Domain.Auth;
using CampusBite.Domain.Common.Exceptions;

namespace CampusBite.Application.Tests.Auth;

// JJ-Sprint5 13/11/2026: reglas de cambiar la contraseña con la sesión iniciada y respuesta con nombre/idCafeteria
public class CambiarPasswordTests
{
    private readonly FakeUsuarioRepository _repositorio = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly AuthService _servicio;
    private readonly Guid _id = Guid.NewGuid();

    public CambiarPasswordTests()
    {
        _servicio = new AuthService(_repositorio, new FakeTokenService(), _hasher, new FakeEnviadorCorreo(),
            new FakeLogger<AuthService>());
        _repositorio.Usuarios["ana@uttt.edu.mx"] = new CredencialesUsuario
        {
            IdUsuario = _id, IdRol = RolUsuario.AdminCafeteria, ContrasenaHash = "hash:Actual123", Activo = true,
            CorreoVerificado = true, Nombre = "Ana", IdCafeteria = Guid.NewGuid()
        };
    }

    [Fact]
    public async Task Cambia_el_hash_cuando_la_actual_es_correcta()
    {
        await _servicio.CambiarPasswordAsync(_id, new CambiarPasswordRequestDto { PasswordActual = "Actual123", PasswordNueva = "Nueva12345" });

        Assert.Equal("hash:Nueva12345", _repositorio.Usuarios["ana@uttt.edu.mx"].ContrasenaHash);
    }

    [Fact]
    public async Task Rechaza_si_la_actual_no_coincide()
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => _servicio.CambiarPasswordAsync(_id,
            new CambiarPasswordRequestDto { PasswordActual = "Otra1234", PasswordNueva = "Nueva12345" }));

        Assert.Equal("La contraseña actual no es correcta.", error.Message);
    }

    [Fact]
    public async Task Rechaza_si_la_nueva_es_igual_a_la_actual()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => _servicio.CambiarPasswordAsync(_id,
            new CambiarPasswordRequestDto { PasswordActual = "Actual123", PasswordNueva = "Actual123" }));
    }

    [Fact]
    public async Task Sesion_de_un_usuario_inexistente_o_inactivo_es_401()
    {
        await Assert.ThrowsAsync<UnauthorizedException>(() => _servicio.CambiarPasswordAsync(Guid.NewGuid(),
            new CambiarPasswordRequestDto { PasswordActual = "Actual123", PasswordNueva = "Nueva12345" }));

        _repositorio.Usuarios["ana@uttt.edu.mx"].Activo = false;
        await Assert.ThrowsAsync<UnauthorizedException>(() => _servicio.CambiarPasswordAsync(_id,
            new CambiarPasswordRequestDto { PasswordActual = "Actual123", PasswordNueva = "Nueva12345" }));
    }

    [Fact]
    public async Task Login_devuelve_nombre_e_id_de_cafeteria()
    {
        var sesion = await _servicio.LoginAsync(new LoginRequestDto { Correo = "ANA@uttt.edu.mx", Password = "Actual123" });

        Assert.Equal("Ana", sesion.Nombre);
        Assert.Equal(_repositorio.Usuarios["ana@uttt.edu.mx"].IdCafeteria, sesion.IdCafeteria);
    }
}
