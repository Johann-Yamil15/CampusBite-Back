using CampusBite.Application.Auth.Dtos;

namespace CampusBite.Application.Auth;

// JJ-Sprint1 02/10/2026: casos de uso de autenticación que implementa AuthService
public interface IAuthService
{
    Task<AuthResponseDto> RegistrarAsync(RegistroRequestDto dto, CancellationToken cancellationToken = default);

    // JJ-Sprint3 22/10/2026: TEC-11. Autorregistro de un negocio: el usuario nace AdminCafeteria con sesión iniciada
    Task<AuthResponseDto> RegistrarCafeteriaAsync(RegistroCafeteriaRequestDto dto, CancellationToken cancellationToken = default);

    // JJ-Sprint3 21/10/2026: TEC-12. Siempre "éxito" sin importar si el correo existe (no se revela)
    Task RestablecerPasswordAsync(RestablecerPasswordRequestDto dto, CancellationToken cancellationToken = default);

    Task<AuthResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken cancellationToken = default);
}
