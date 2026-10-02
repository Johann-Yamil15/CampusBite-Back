using CampusBite_Back.Models.Entities;

namespace CampusBite_Back.Interfaces;

// JJ-Sprint1 02/10/2026: contrato para generar tokens JWT
public interface ITokenService
{
    (string Token, DateTime ExpiraEn) GenerarToken(Usuario usuario);
}
