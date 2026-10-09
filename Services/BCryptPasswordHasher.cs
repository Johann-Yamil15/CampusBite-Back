using CampusBite_Back.Interfaces;

namespace CampusBite_Back.Services;

// JJ-Sprint2 09/10/2026: hashing de contraseñas con BCrypt (costo 12) y verificación de tiempo constante
public class BCryptPasswordHasher : IPasswordHasher
{
    // Cada +1 duplica el tiempo de cálculo; 12 tarda ~250 ms y frena ataques de fuerza bruta.
    // Los hashes creados con otro costo (p. ej. 11) se siguen verificando sin cambios.
    private const int WorkFactor = 12;

    // JJ-Sprint2 09/10/2026: hash de relleno para que un correo inexistente tarde lo mismo que uno real
    private static readonly string HashFicticio = BCrypt.Net.BCrypt.HashPassword("campusbite-hash-ficticio", WorkFactor);

    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string? hash)
    {
        if (string.IsNullOrEmpty(hash))
        {
            BCrypt.Net.BCrypt.Verify(password, HashFicticio);
            return false;
        }

        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Hash con formato inválido (p. ej. los marcadores del seed): se trata como contraseña incorrecta
            BCrypt.Net.BCrypt.Verify(password, HashFicticio);
            return false;
        }
    }
}
