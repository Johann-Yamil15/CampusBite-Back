namespace CampusBite.Application.Auth.Dtos;

// JJ-Sprint5 13/11/2026: GET /api/auth/me. Se lee de la BD (no del token) para que el nombre esté al día tras editar el perfil
public class MeDto
{
    public Guid Id { get; init; }
    public required string Correo { get; init; }
    public required string Rol { get; init; }
    public required string Nombre { get; init; }
    public Guid? IdCafeteria { get; init; }
}
