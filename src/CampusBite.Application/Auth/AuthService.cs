using CampusBite.Application.Auth.Dtos;
using CampusBite.Application.Common.Security;
using CampusBite.Domain.Auth;
using CampusBite.Domain.Common.Exceptions;

namespace CampusBite.Application.Auth;

// JJ-Sprint2 09/10/2026: reglas de registro e inicio de sesión; los datos vienen de IUsuarioRepository (sin SQL aquí)
public class AuthService : IAuthService
{
    private readonly IUsuarioRepository _usuarios;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher _passwordHasher;

    public AuthService(IUsuarioRepository usuarios, ITokenService tokenService, IPasswordHasher passwordHasher)
    {
        _usuarios = usuarios;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
    }

    public async Task<AuthResponseDto> RegistrarAsync(RegistroRequestDto dto, CancellationToken cancellationToken = default)
    {
        var correo = NormalizarCorreo(dto.Correo);
        var hash = _passwordHasher.Hash(dto.Password);
        var matricula = string.IsNullOrWhiteSpace(dto.Matricula) ? null : dto.Matricula.Trim();
        var telefono = string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono.Trim();

        var idUsuario = await _usuarios.RegistrarAsync(dto.Nombre.Trim(), correo, hash, matricula, telefono, cancellationToken);

        // JJ-Sprint1 04/10/2026: el registro público siempre crea alumnos (id_rol = 1)
        return CrearRespuesta(idUsuario, correo, RolUsuario.Alumno);
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

        return CrearRespuesta(credenciales.IdUsuario, correo, credenciales.IdRol);
    }

    private static string NormalizarCorreo(string correo) => correo.Trim().ToLowerInvariant();

    private AuthResponseDto CrearRespuesta(Guid idUsuario, string correo, RolUsuario rol)
    {
        var (token, expiraEn) = _tokenService.GenerarToken(idUsuario, correo, rol);

        return new AuthResponseDto
        {
            UsuarioId = idUsuario,
            Correo = correo,
            Rol = rol.ToString(),
            Token = token,
            ExpiraEn = expiraEn
        };
    }
}
