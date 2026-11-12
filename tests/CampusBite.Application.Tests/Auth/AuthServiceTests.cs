using CampusBite.Application.Auth;
using CampusBite.Application.Auth.Dtos;
using CampusBite.Domain.Auth;
using CampusBite.Domain.Cafeterias;
using CampusBite.Domain.Common.Exceptions;
using CampusBite.Application.Tests.Productos;
using Microsoft.Extensions.Logging;

namespace CampusBite.Application.Tests.Auth;

// JJ-Sprint2 09/10/2026: pruebas de las reglas de registro e inicio de sesión de AuthService
public class AuthServiceTests
{
    private readonly FakeUsuarioRepository _repositorio = new();
    private readonly FakePasswordHasher _hasher = new();
    private readonly FakeEnviadorCorreo _correo = new();
    private readonly FakeLogger<AuthService> _logger = new();
    private readonly AuthService _servicio;

    public AuthServiceTests()
    {
        _servicio = new AuthService(_repositorio, new FakeTokenService(), _hasher, _correo, _logger);
    }

    [Fact]
    public async Task Registrar_NormalizaDatos_GuardaHash_YMandaElCorreoDeVerificacionSinIniciarSesion()
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
        Assert.True(respuesta.CorreoEnviado);
        Assert.Contains("Te enviamos un correo", respuesta.Mensaje);
        var enviado = Assert.Single(_correo.Enviados);
        Assert.Equal(("ana@uttt.edu.mx", "Ana López"), (enviado.Correo, enviado.Nombre));
        // En la BD solo queda el hash del token que va en el correo
        Assert.Equal(TokenVerificacion.Hash(enviado.Token), Assert.Single(_repositorio.Tokens.Keys));
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
            IdUsuario = id, IdRol = RolUsuario.AdminSistema, ContrasenaHash = "hash:Password123", Activo = true,
            CorreoVerificado = true
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
        Assert.True(respuesta.CorreoEnviado);
        Assert.Equal(("ana@negocio.mx", "Ana López"), (_correo.Enviados[0].Correo, _correo.Enviados[0].Nombre));
    }

    [Fact]
    public async Task RegistrarCafeteria_CorreoONombreRepetido_PropagaConflict()
    {
        _repositorio.ErrorAlRegistrarCafeteria = new ConflictException("Ya existe una cafetería con el nombre \"Café Norte\"");

        var error = await Assert.ThrowsAsync<ConflictException>(() => _servicio.RegistrarCafeteriaAsync(NuevoNegocio()));

        Assert.Contains("Café Norte", error.Message);
    }

    [Fact]
    public async Task RegistrarCafeteria_ElEncargadoPuedeIniciarSesionDespuesDeVerificar()
    {
        await _servicio.RegistrarCafeteriaAsync(NuevoNegocio());
        await _servicio.VerificarCorreoAsync(new VerificarCorreoRequestDto { Token = _correo.Enviados[0].Token });

        var sesion = await _servicio.LoginAsync(new LoginRequestDto { Correo = "ANA@negocio.mx", Password = "Password123" });

        Assert.Equal(nameof(RolUsuario.AdminCafeteria), sesion.Rol);
    }

    // ---- JY-Sprint4 31/10/2026: verificación de correo ----

    private Task<RegistroPendienteDto> RegistrarAna() => _servicio.RegistrarAsync(new RegistroRequestDto
    {
        Nombre = "Ana", Correo = "ana@uttt.edu.mx", Password = "Password123"
    });

    [Fact]
    public async Task Login_SinVerificarElCorreo_Lanza403_SoloSiLaPasswordEsCorrecta()
    {
        await RegistrarAna();

        var error = await Assert.ThrowsAsync<ForbiddenException>(() =>
            _servicio.LoginAsync(new LoginRequestDto { Correo = "ana@uttt.edu.mx", Password = "Password123" }));
        Assert.Contains("Verifica tu correo", error.Message);

        // Con la contraseña mal sigue siendo el 401 de siempre: no se revela que la cuenta existe
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _servicio.LoginAsync(new LoginRequestDto { Correo = "ana@uttt.edu.mx", Password = "MalaClave1" }));
    }

    [Fact]
    public async Task VerificarCorreo_ConElTokenDelCorreo_IniciaSesion_YDespuesYaPuedeEntrar()
    {
        await RegistrarAna();

        var sesion = await _servicio.VerificarCorreoAsync(new VerificarCorreoRequestDto { Token = $"  {_correo.Enviados[0].Token} " });

        Assert.Equal(("ana@uttt.edu.mx", nameof(RolUsuario.Alumno)), (sesion.Correo, sesion.Rol));
        Assert.StartsWith("token:", sesion.Token);
        var login = await _servicio.LoginAsync(new LoginRequestDto { Correo = "ana@uttt.edu.mx", Password = "Password123" });
        Assert.Equal(sesion.UsuarioId, login.UsuarioId);
    }

    [Fact]
    public async Task VerificarCorreo_ConTokenInventado_LanzaBusinessRule()
    {
        await RegistrarAna();

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _servicio.VerificarCorreoAsync(new VerificarCorreoRequestDto { Token = "inventado" }));
    }

    [Fact]
    public async Task Reenviar_MandaUnTokenNuevo_YElAnteriorDejaDeServir()
    {
        await RegistrarAna();
        var primero = _correo.Enviados[0].Token;

        await _servicio.ReenviarVerificacionAsync(new ReenviarVerificacionRequestDto { Correo = " ANA@uttt.edu.mx " });

        Assert.Equal(2, _correo.Enviados.Count);
        Assert.NotEqual(primero, _correo.Enviados[1].Token);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _servicio.VerificarCorreoAsync(new VerificarCorreoRequestDto { Token = primero }));
        await _servicio.VerificarCorreoAsync(new VerificarCorreoRequestDto { Token = _correo.Enviados[1].Token });
    }

    [Fact]
    public async Task Reenviar_ACorreoInexistenteOYaVerificado_NoMandaNadaNiFalla()
    {
        await RegistrarAna();
        await _servicio.VerificarCorreoAsync(new VerificarCorreoRequestDto { Token = _correo.Enviados[0].Token });

        await _servicio.ReenviarVerificacionAsync(new ReenviarVerificacionRequestDto { Correo = "ana@uttt.edu.mx" });
        await _servicio.ReenviarVerificacionAsync(new ReenviarVerificacionRequestDto { Correo = "nadie@uttt.edu.mx" });

        Assert.Single(_correo.Enviados);
    }

    [Fact]
    public async Task Registrar_SiElCorreoNoSale_LaCuentaQuedaCreada_YAvisaQuePidaOtro()
    {
        _correo.Error = new ServiceUnavailableException("El servicio de correo no está disponible en este momento.");

        var respuesta = await RegistrarAna();

        Assert.False(respuesta.CorreoEnviado);
        Assert.Contains("no pudimos enviar", respuesta.Mensaje);
        Assert.NotNull(_repositorio.UltimoRegistro);
        var aviso = Assert.Single(_logger.Registros);
        Assert.Equal(LogLevel.Warning, aviso.Nivel);
        Assert.Contains("ana@uttt.edu.mx", aviso.Mensaje);
    }

    [Fact]
    public async Task Reenviar_SiElCorreoNoSale_NoLanzaError()
    {
        await RegistrarAna();
        _correo.Error = new ServiceUnavailableException("No disponible");

        await _servicio.ReenviarVerificacionAsync(new ReenviarVerificacionRequestDto { Correo = "ana@uttt.edu.mx" });

        Assert.Single(_logger.Registros);
    }

    [Fact]
    public void TokenVerificacion_EsAleatorio_DeUrl_YSuHashEsSha256Hex()
    {
        var (token1, hash1) = TokenVerificacion.Generar();
        var (token2, _) = TokenVerificacion.Generar();

        Assert.NotEqual(token1, token2);
        Assert.Equal(43, token1.Length);                                     // 32 bytes en Base64Url sin relleno
        Assert.Matches("^[A-Za-z0-9_-]+$", token1);
        Assert.Matches("^[0-9a-f]{64}$", hash1);
        Assert.Equal(hash1, TokenVerificacion.Hash(token1));
        Assert.Equal(TimeSpan.FromHours(24), TokenVerificacion.Vigencia);
    }
}
