namespace CampusBite_Back.Interfaces;

// JJ-Sprint2 09/10/2026: contrato para cifrar y verificar contraseñas (implementación con BCrypt)
public interface IPasswordHasher
{
    string Hash(string password);

    // Recibe null cuando el usuario no existe: igual consume el mismo tiempo y devuelve false
    bool Verify(string password, string? hash);
}
