using CampusBite_Back.Models.Enums;

namespace CampusBite_Back.Interfaces;

// JJ-Sprint1 04/10/2026: contrato para generar tokens JWT
public interface ITokenService
{
    (string Token, DateTime ExpiraEn) GenerarToken(Guid idUsuario, string correo, RolUsuario rol);
}
