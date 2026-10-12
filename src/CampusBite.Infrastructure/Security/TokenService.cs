using System.Text;
using CampusBite.Application.Common.Security;
using CampusBite.Domain.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CampusBite.Infrastructure.Security;

// JJ-Sprint2 09/10/2026: genera el JWT con claims cortos (sub, email, role) que el front puede leer directo
public class TokenService : ITokenService
{
    // Nombre del claim de rol dentro del token; el front lo usa en sus guards de ruta
    public const string ClaimRol = "role";

    private readonly JwtSettings _jwt;

    public TokenService(IOptions<JwtSettings> jwt)
    {
        _jwt = jwt.Value;
    }

    public (string Token, DateTime ExpiraEn) GenerarToken(Guid idUsuario, string correo, RolUsuario rol)
    {
        var ahora = DateTime.UtcNow;
        var expiraEn = ahora.AddMinutes(_jwt.ExpirationMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            IssuedAt = ahora,
            NotBefore = ahora,
            Expires = expiraEn,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = idUsuario.ToString(),
                [JwtRegisteredClaimNames.Email] = correo,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
                [ClaimRol] = rol.ToString()
            },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key)), SecurityAlgorithms.HmacSha256)
        };

        return (new JsonWebTokenHandler().CreateToken(descriptor), expiraEn);
    }
}
