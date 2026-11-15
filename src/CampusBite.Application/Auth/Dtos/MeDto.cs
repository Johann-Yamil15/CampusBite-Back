namespace CampusBite.Application.Auth.Dtos;

// JJ-Sprint5 13/11/2026: GET /api/auth/me, leído de los claims del token (sin ir a la BD).
// Correo es null si el token no trae el claim, como antes del Sprint 5.
public class MeDto
{
    public Guid Id { get; init; }
    public string? Correo { get; init; }
    public required string Rol { get; init; }
    public required string Nombre { get; init; }
    public Guid? IdCafeteria { get; init; }
}
