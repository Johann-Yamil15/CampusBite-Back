using CampusBite_Back.Common.Exceptions;
using CampusBite_Back.Data;
using CampusBite_Back.Interfaces;
using CampusBite_Back.Models.Dtos.Auth;
using CampusBite_Back.Models.Entities;
using CampusBite_Back.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CampusBite_Back.Services;

// JJ-Sprint1 04/10/2026: registro e inicio de sesión usando sp_registrar_usuario y sp_obtener_credenciales
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
        var hash = BCrypt.Net.BCrypt.HashPassword(dto.Password);
        var matricula = string.IsNullOrWhiteSpace(dto.Matricula) ? null : dto.Matricula.Trim();
        var telefono = string.IsNullOrWhiteSpace(dto.Telefono) ? null : dto.Telefono.Trim();

        Guid idUsuario;
        try
        {
            idUsuario = await _context.Database
                .SqlQuery<Guid>($"""
                    SELECT sp_registrar_usuario({dto.Nombre}::varchar, {correo}::varchar, {hash}::varchar,
                                                {matricula}::varchar, {telefono}::varchar) AS "Value"
                    """)
                .SingleAsync();
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw ApiException.Conflict("El correo o la matrícula ya están registrados.");
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.CheckViolation)
        {
            throw new ApiException("Los datos no cumplen el formato requerido (nombre, correo o teléfono).");
        }

        // JJ-Sprint1 04/10/2026: sp_registrar_usuario siempre crea alumnos (id_rol = 1)
        return CrearRespuesta(idUsuario, correo, RolUsuario.Alumno);
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto dto)
    {
        var correo = dto.Correo.Trim().ToLowerInvariant();

        var credenciales = await _context.Database
            .SqlQuery<CredencialesUsuario>($"""
                SELECT id_usuario, id_rol, contrasena_hash, activo
                  FROM sp_obtener_credenciales({correo}::varchar)
                """)
            .SingleOrDefaultAsync();

        if (credenciales is null || !credenciales.Activo || !VerificarPassword(dto.Password, credenciales.ContrasenaHash))
        {
            throw ApiException.Unauthorized("Correo o contraseña incorrectos.");
        }

        return CrearRespuesta(credenciales.IdUsuario, correo, credenciales.IdRol);
    }

    // JJ-Sprint1 04/10/2026: un hash con formato inválido (p. ej. los del seed) se trata como contraseña incorrecta
    private static bool VerificarPassword(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            return false;
        }
    }

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
