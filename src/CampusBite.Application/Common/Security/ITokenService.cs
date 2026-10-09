using CampusBite.Domain.Auth;

namespace CampusBite.Application.Common.Security;

// JJ-Sprint1 04/10/2026: contrato para generar tokens JWT (lo implementa Infrastructure)
public interface ITokenService
{
    (string Token, DateTime ExpiraEn) GenerarToken(Guid idUsuario, string correo, RolUsuario rol);
}
