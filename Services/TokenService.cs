using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CampusBite_Back.Common.Settings;
using CampusBite_Back.Interfaces;
using CampusBite_Back.Models.Enums;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CampusBite_Back.Services;

// JJ-Sprint1 04/10/2026: genera el JWT firmado con id (UUID), correo y rol del usuario
public class TokenService : ITokenService
{
    private readonly JwtSettings _jwt;

    public TokenService(IOptions<JwtSettings> jwt)
    {
        _jwt = jwt.Value;
    }

    public (string Token, DateTime ExpiraEn) GenerarToken(Guid idUsuario, string correo, RolUsuario rol)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, idUsuario.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.NameIdentifier, idUsuario.ToString()),
            new Claim(ClaimTypes.Email, correo),
            new Claim(ClaimTypes.Role, rol.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
        var expiraEn = DateTime.UtcNow.AddMinutes(_jwt.ExpirationMinutes);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: expiraEn,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expiraEn);
    }
}
