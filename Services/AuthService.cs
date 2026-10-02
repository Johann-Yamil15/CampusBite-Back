using CampusBite_Back.Common.Exceptions;
using CampusBite_Back.Data;
using CampusBite_Back.Interfaces;
using CampusBite_Back.Models.Dtos.Auth;
using CampusBite_Back.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace CampusBite_Back.Services;

// JJ-Sprint1 02/10/2026: lógica de registro e inicio de sesión; implementa IAuthService
public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly ITokenService _tokenService;

    public AuthService(AppDbContext context, ITokenService tokenService)
    {
        _context = context;
        _tokenService = tokenService;
    }

    public async Task<AuthResponseDto> RegistrarAsync(RegistroRequestDto dto)
    {
        var correo = dto.Correo.Trim().ToLowerInvariant();

        if (await _context.Usuarios.AnyAsync(u => u.Correo == correo))
        {
            throw ApiException.Conflict("El correo ya está registrado.");
        }

        var usuario = new Usuario
        {
            Nombre = dto.Nombre.Trim(),
            Correo = correo,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password)
        };

        _context.Usuarios.Add(usuario);
        await _context.SaveChangesAsync();

        return CrearRespuesta(usuario);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto dto)
    {
        var correo = dto.Correo.Trim().ToLowerInvariant();
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Correo == correo);

        if (usuario is null || !usuario.Activo || !BCrypt.Net.BCrypt.Verify(dto.Password, usuario.PasswordHash))
        {
            throw ApiException.Unauthorized("Correo o contraseña incorrectos.");
        }

        return CrearRespuesta(usuario);
    }

    private AuthResponseDto CrearRespuesta(Usuario usuario)
    {
        var (token, expiraEn) = _tokenService.GenerarToken(usuario);

        return new AuthResponseDto
        {
            UsuarioId = usuario.Id,
            Nombre = usuario.Nombre,
            Correo = usuario.Correo,
            Rol = usuario.Rol.ToString(),
            Token = token,
            ExpiraEn = expiraEn
        };
    }
}
