using CampusBite.Application.Auth.Dtos;
using CampusBite.Application.Common.Correo;
using CampusBite.Application.Common.Security;
using CampusBite.Domain.Auth;
using CampusBite.Domain.Common.Exceptions;
using Microsoft.Extensions.Logging;

namespace CampusBite.Application.Auth;

// JJ-Sprint2 09/10/2026: reglas de registro e inicio de sesión; los datos vienen de IUsuarioRepository (sin SQL aquí)
public class AuthService : IAuthService
{
    private readonly IUsuarioRepository _usuarios;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEnviadorCorreo _correo;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUsuarioRepository usuarios, ITokenService tokenService, IPasswordHasher passwordHasher,
        IEnviadorCorreo correo, ILogger<AuthService> logger)
    {
        _usuarios = usuarios;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
        _correo = correo;
        _logger = logger;
    }

    public async Task<RegistroPendienteDto> RegistrarAsync(RegistroRequestDto dto, CancellationToken cancellationToken = default)
    {
        var correo = NormalizarCorreo(dto.Correo);
        var hash = _passwordHasher.Hash(dto.Password);
        var matricula = string.IsNullOrWhiteSpace(dto.Matricula) ? null : dto.Matricula.Trim();
        var telefono = string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono.Trim();

        // JJ-Sprint1 04/10/2026: el registro público siempre crea alumnos (id_rol = 1)
        await _usuarios.RegistrarAsync(dto.Nombre.Trim(), correo, hash, matricula, telefono, cancellationToken);

        return await MandarVerificacionAsync(correo, cancellationToken);
    }

    // JJ-Sprint3 22/10/2026: TEC-11. Mismo patrón que RegistrarAsync, pero el usuario nace AdminCafeteria y encargado
    // de la cafetería que se crea con él (el procedimiento hace las dos cosas en una sola transacción)
    public async Task<RegistroPendienteDto> RegistrarCafeteriaAsync(RegistroCafeteriaRequestDto dto, CancellationToken cancellationToken = default)
    {
        var correo = NormalizarCorreo(dto.Correo);
        var hash = _passwordHasher.Hash(dto.Password);
        var telefono = string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono.Trim();

        await _usuarios.RegistrarCafeteriaAsync(
            dto.NombreEncargado.Trim(), correo, hash, telefono,
            dto.NombreCafeteria.Trim(), dto.Ubicacion.Trim(), dto.EsDelCampus!.Value, dto.PuntoEntrega.Trim(),
            dto.MetodosPago.Distinct().ToList(), cancellationToken);

        return await MandarVerificacionAsync(correo, cancellationToken);
    }

    public async Task<AuthResponseDto> VerificarCorreoAsync(VerificarCorreoRequestDto dto, CancellationToken cancellationToken = default)
    {
        var (idUsuario, correo, rol) = await _usuarios.VerificarCorreoAsync(
            TokenVerificacion.Hash(dto.Token.Trim()), cancellationToken);
        // JJ-Sprint5 13/11/2026: la respuesta lleva nombre e idCafeteria, como el login
        var usuario = await _usuarios.ObtenerPorIdAsync(idUsuario, cancellationToken);
        return CrearRespuesta(idUsuario, correo, rol, usuario?.Nombre ?? string.Empty, usuario?.IdCafeteria);
    }

    public async Task ReenviarVerificacionAsync(ReenviarVerificacionRequestDto dto, CancellationToken cancellationToken = default)
    {
        await EnviarSiCorrespondeAsync(NormalizarCorreo(dto.Correo), cancellationToken);
    }

    // La cuenta ya quedó creada: si el correo no sale, no se deshace el registro; se avisa para que pida otro
    private async Task<RegistroPendienteDto> MandarVerificacionAsync(string correo, CancellationToken cancellationToken)
    {
        var enviado = await EnviarSiCorrespondeAsync(correo, cancellationToken);
        return new RegistroPendienteDto
        {
            Correo = correo,
            CorreoEnviado = enviado,
            Mensaje = enviado
                ? "Te enviamos un correo para verificar tu cuenta. Abre el enlace para poder iniciar sesión."
                : "Tu cuenta se creó, pero no pudimos enviar el correo de verificación. Pide que te lo reenviemos."
        };
    }

    // true si se mandó el correo. No lanza error si la cuenta no existe, ya está verificada o el correo falla:
    // así la respuesta de "reenviar" no revela qué cuentas existen
    private async Task<bool> EnviarSiCorrespondeAsync(string correo, CancellationToken cancellationToken)
    {
        var (token, hash) = TokenVerificacion.Generar();
        var nombre = await _usuarios.GenerarVerificacionAsync(
            correo, hash, DateTimeOffset.UtcNow.Add(TokenVerificacion.Vigencia), cancellationToken);
        if (nombre is null)
        {
            return false;
        }

        try
        {
            await _correo.EnviarVerificacionAsync(correo, nombre, token, cancellationToken);
            return true;
        }
        catch (ServiceUnavailableException ex)
        {
            _logger.LogWarning(ex, "No se pudo enviar el correo de verificación a {Correo}", correo);
            return false;
        }
    }

    // JJ-Sprint3 21/10/2026: TEC-12. No lanza error si el correo no existe: siempre "éxito" para no revelarlo
    public async Task RestablecerPasswordAsync(RestablecerPasswordRequestDto dto, CancellationToken cancellationToken = default)
    {
        var correo = NormalizarCorreo(dto.Correo);
        var hash = _passwordHasher.Hash(dto.NuevaPassword);
        await _usuarios.RestablecerPasswordAsync(correo, hash, cancellationToken);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken cancellationToken = default)
    {
        var correo = NormalizarCorreo(dto.Correo);
        var credenciales = await _usuarios.ObtenerCredencialesAsync(correo, cancellationToken);

        // JJ-Sprint2 09/10/2026: se verifica siempre (aunque el correo no exista) para no revelar por tiempo qué correos están registrados
        var passwordValida = _passwordHasher.Verify(dto.Password, credenciales?.ContrasenaHash);

        if (credenciales is null || !credenciales.Activo || !passwordValida)
        {
            throw new UnauthorizedException("Correo o contraseña incorrectos.");
        }

        // JY-Sprint4 31/10/2026: se revisa después de la contraseña para no revelar qué correos existen
        if (!credenciales.CorreoVerificado)
        {
            throw new ForbiddenException(
                "Verifica tu correo antes de iniciar sesión. Revisa tu bandeja de entrada o pide que te reenviemos el enlace.");
        }

        return CrearRespuesta(credenciales.IdUsuario, correo, credenciales.IdRol, credenciales.Nombre, credenciales.IdCafeteria);
    }

    // JJ-Sprint5 13/11/2026: el front muestra "La contraseña actual no es correcta." en ese campo
    public async Task CambiarPasswordAsync(Guid idUsuario, CambiarPasswordRequestDto dto, CancellationToken cancellationToken = default)
    {
        var usuario = await ObtenerActivoAsync(idUsuario, cancellationToken);
        if (!_passwordHasher.Verify(dto.PasswordActual, usuario.ContrasenaHash))
        {
            throw new BusinessRuleException("La contraseña actual no es correcta.");
        }
        if (dto.PasswordNueva == dto.PasswordActual)
        {
            throw new BusinessRuleException("La contraseña nueva debe ser distinta de la actual.");
        }

        await _usuarios.CambiarPasswordAsync(idUsuario, _passwordHasher.Hash(dto.PasswordNueva), cancellationToken);
    }

    private async Task<CredencialesUsuario> ObtenerActivoAsync(Guid idUsuario, CancellationToken cancellationToken)
    {
        var usuario = await _usuarios.ObtenerPorIdAsync(idUsuario, cancellationToken);
        return usuario is { Activo: true }
            ? usuario
            : throw new UnauthorizedException("La sesión no es válida. Inicia sesión de nuevo.");
    }

    private static string NormalizarCorreo(string correo) => correo.Trim().ToLowerInvariant();

    private AuthResponseDto CrearRespuesta(Guid idUsuario, string correo, RolUsuario rol, string nombre, Guid? idCafeteria)
    {
        var (token, expiraEn) = _tokenService.GenerarToken(idUsuario, correo, rol, nombre, idCafeteria);

        return new AuthResponseDto
        {
            UsuarioId = idUsuario,
            Correo = correo,
            Rol = rol.ToString(),
            Nombre = nombre,
            IdCafeteria = idCafeteria,
            Token = token,
            ExpiraEn = expiraEn
        };
    }
}
