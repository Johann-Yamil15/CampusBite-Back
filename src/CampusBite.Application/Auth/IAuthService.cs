using CampusBite.Application.Auth.Dtos;

namespace CampusBite.Application.Auth;

// JJ-Sprint1 02/10/2026: casos de uso de autenticación que implementa AuthService
public interface IAuthService
{
    Task<AuthResponseDto> RegistrarAsync(RegistroRequestDto dto, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken cancellationToken = default);
}
