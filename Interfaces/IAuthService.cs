using CampusBite_Back.Models.Dtos.Auth;

namespace CampusBite_Back.Interfaces;

// JJ-Sprint1 02/10/2026: contrato de autenticación que implementa AuthService
public interface IAuthService
{
    Task<AuthResponseDto> RegistrarAsync(RegistroRequestDto dto);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto dto);
}
