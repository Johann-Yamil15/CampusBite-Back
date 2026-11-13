using CampusBite.Application.Auth.Dtos;

namespace CampusBite.Application.Auth;

// JJ-Sprint1 02/10/2026: casos de uso de autenticación que implementa AuthService
public interface IAuthService
{
    // JY-Sprint4 31/10/2026: los dos registros ya no inician sesión: mandan el correo de verificación
    Task<RegistroPendienteDto> RegistrarAsync(RegistroRequestDto dto, CancellationToken cancellationToken = default);

    // JJ-Sprint3 22/10/2026: TEC-11. Autorregistro de un negocio: el usuario nace AdminCafeteria con sesión iniciada
    Task<RegistroPendienteDto> RegistrarCafeteriaAsync(RegistroCafeteriaRequestDto dto, CancellationToken cancellationToken = default);

    // JY-Sprint4 31/10/2026: con el token del enlace verifica el correo e inicia la sesión
    Task<AuthResponseDto> VerificarCorreoAsync(VerificarCorreoRequestDto dto, CancellationToken cancellationToken = default);

    // JY-Sprint4 31/10/2026: manda otro correo si la cuenta existe y no está verificada; nunca dice si existe
    Task ReenviarVerificacionAsync(ReenviarVerificacionRequestDto dto, CancellationToken cancellationToken = default);

    // JJ-Sprint3 21/10/2026: TEC-12. Siempre "éxito" sin importar si el correo existe (no se revela)
    Task RestablecerPasswordAsync(RestablecerPasswordRequestDto dto, CancellationToken cancellationToken = default);

    Task<AuthResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken cancellationToken = default);

    // JJ-Sprint5 13/11/2026: cambio de contraseña con la actual
    Task CambiarPasswordAsync(Guid idUsuario, CambiarPasswordRequestDto dto, CancellationToken cancellationToken = default);
}
