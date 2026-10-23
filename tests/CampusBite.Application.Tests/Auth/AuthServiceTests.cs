using CampusBite.Application.Auth;
using CampusBite.Application.Auth.Dtos;
using CampusBite.Domain.Auth;
using CampusBite.Domain.Cafeterias;
using CampusBite.Domain.Common.Exceptions;

namespace CampusBite.Application.Tests.Auth;

// JJ-Sprint2 09/10/2026: pruebas de las reglas de registro e inicio de sesión de AuthService
public class AuthServiceTests
{
    private readonly FakeUsuarioRepository _repositorio = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly AuthService _servicio;

    public AuthServiceTests()
    {
        _servicio = new AuthService(_repositorio, new FakeTokenService(), _hasher);
    }

    [Fact]
    public async Task Registrar_NormalizaDatos_GuardaHashYDevuelveAlumnoConToken()
    {
        var respuesta = await _servicio.RegistrarAsync(new RegistroRequestDto
        {
            Nombre = "  Ana López ", Correo = "  Ana@UTTT.edu.mx ", Password = "Password123",
            Matricula = "   ", Telefono = " 7711234567 "
        });

        var registro = _repositorio.UltimoRegistro!.Value;
        Assert.Equal("Ana López", registro.Nombre);
        Assert.Equal("ana@uttt.edu.mx", registro.Correo);
        Assert.Equal("hash:Password123", registro.Hash);
        Assert.Null(registro.Matricula);
        Assert.Equal("7711234567", registro.Telefono);

        Assert.Equal("ana@uttt.edu.mx", respuesta.Correo);
        Assert.Equal(nameof(RolUsuario.Alumno), respuesta.Rol);
        Assert.StartsWith("token:", respuesta.Token);
    }

    [Fact]
    public async Task Registrar_CorreoDuplicado_PropagaConflict()
    {
        _repositorio.ErrorAlRegistrar = new ConflictException("El correo o la matrícula ya están registrados.");

        await Assert.ThrowsAsync<ConflictException>(() => _servicio.RegistrarAsync(new RegistroRequestDto
        {
            Nombre = "Ana", Correo = "ana@uttt.edu.mx", Password = "Password123"
        }));
    }

    [Fact]
    public async Task Login_CredencialesCorrectas_DevuelveRolDelUsuario()
    {
        var id = Guid.NewGuid();
        _repositorio.Usuarios["admin@uttt.edu.mx"] = new CredencialesUsuario
        {
            IdUsuario = id, IdRol = RolUsuario.AdminSistema, ContrasenaHash = "hash:Password123", Activo = true
        };

        var respuesta = await _servicio.LoginAsync(new LoginRequestDto { Correo = " ADMIN@uttt.edu.mx", Password = "Password123" });

        Assert.Equal(id, respuesta.UsuarioId);
        Assert.Equal(nameof(RolUsuario.AdminSistema), respuesta.Rol);
    }

    [Fact]
    public async Task Login_PasswordIncorrecta_LanzaUnauthorized()
    {
        _repositorio.Usuarios["ana@uttt.edu.mx"] = new CredencialesUsuario
        {
            IdUsuario = Guid.NewGuid(), IdRol = RolUsuario.Alumno, ContrasenaHash = "hash:Password123", Activo = true
        };

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _servicio.LoginAsync(new LoginRequestDto { Correo = "ana@uttt.edu.mx", Password = "MalaClave1" }));
    }

    [Fact]
    public async Task Login_UsuarioInactivo_LanzaUnauthorizedAunqueLaPasswordSeaCorrecta()
    {
        _repositorio.Usuarios["ana@uttt.edu.mx"] = new CredencialesUsuario
        {
            IdUsuario = Guid.NewGuid(), IdRol = RolUsuario.Alumno, ContrasenaHash = "hash:Password123", Activo = false
        };

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _servicio.LoginAsync(new LoginRequestDto { Correo = "ana@uttt.edu.mx", Password = "Password123" }));
    }

    [Fact]
    public async Task Login_CorreoInexistente_IgualVerificaPasswordYLanzaElMismoError()
    {
        var error = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _servicio.LoginAsync(new LoginRequestDto { Correo = "nadie@uttt.edu.mx", Password = "Password123" }));

        // Verificar aunque el correo no exista evita revelar por tiempo qué correos están registrados
        Assert.Equal(1, _hasher.Verificaciones);
        Assert.Equal("Correo o contraseña incorrectos.", error.Message);
    }

    // JJ-Sprint3 22/10/2026: TEC-11. Autorregistro de un negocio
    private static RegistroCafeteriaRequestDto NuevoNegocio() => new()
    {
        NombreEncargado = "  Ana López ", Correo = "  Ana@Negocio.MX ", Password = "Password123", Telefono = "   ",
        NombreCafeteria = "  Café Norte ", Ubicacion = " Edificio K ", EsDelCampus = true, PuntoEntrega = " Ventanilla 2 ",
        MetodosPago = [MetodoPago.Efectivo, MetodoPago.Tarjeta, MetodoPago.Efectivo]
    };

    [Fact]
    public async Task RegistrarCafeteria_NormalizaDatosYDevuelveAdminCafeteriaConToken()
    {
        var respuesta = await _servicio.RegistrarCafeteriaAsync(NuevoNegocio());

        var registro = _repositorio.UltimoRegistroCafeteria!.Value;
        Assert.Equal("Ana López", registro.NombreEncargado);
        Assert.Equal("ana@negocio.mx", registro.Correo);
        Assert.Equal("hash:Password123", registro.Hash);
        Assert.Null(registro.Telefono);
        Assert.Equal("Café Norte", registro.NombreCafeteria);
        Assert.Equal("Edificio K", registro.Ubicacion);
        Assert.True(registro.EsDelCampus);
        Assert.Equal("Ventanilla 2", registro.PuntoEntrega);
        Assert.Equal([MetodoPago.Efectivo, MetodoPago.Tarjeta], registro.Metodos);   // sin repetidos

        Assert.Equal("ana@negocio.mx", respuesta.Correo);
        Assert.Equal(nameof(RolUsuario.AdminCafeteria), respuesta.Rol);
        Assert.Contains(nameof(RolUsuario.AdminCafeteria), respuesta.Token);       // el token se emite con el rol nuevo
    }

    [Fact]
    public async Task RegistrarCafeteria_CorreoONombreRepetido_PropagaConflict()
    {
        _repositorio.ErrorAlRegistrarCafeteria = new ConflictException("Ya existe una cafetería con el nombre \"Café Norte\"");

        var error = await Assert.ThrowsAsync<ConflictException>(() => _servicio.RegistrarCafeteriaAsync(NuevoNegocio()));

        Assert.Contains("Café Norte", error.Message);
    }

    [Fact]
    public async Task RegistrarCafeteria_ElEncargadoPuedeIniciarSesionDespues()
    {
        await _servicio.RegistrarCafeteriaAsync(NuevoNegocio());

        var sesion = await _servicio.LoginAsync(new LoginRequestDto { Correo = "ANA@negocio.mx", Password = "Password123" });

        Assert.Equal(nameof(RolUsuario.AdminCafeteria), sesion.Rol);
    }
}
