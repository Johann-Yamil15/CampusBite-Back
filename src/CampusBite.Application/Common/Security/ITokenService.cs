using CampusBite.Domain.Auth;

namespace CampusBite.Application.Common.Security;

// JJ-Sprint1 04/10/2026: contrato para generar tokens JWT (lo implementa Infrastructure)
public interface ITokenService
{
    // JJ-Sprint5 13/11/2026: nombre e idCafeteria viajan como claims (los lee GET /api/auth/me sin ir a la BD)
    (string Token, DateTime ExpiraEn) GenerarToken(Guid idUsuario, string correo, RolUsuario rol,
        string? nombre = null, Guid? idCafeteria = null);
}
