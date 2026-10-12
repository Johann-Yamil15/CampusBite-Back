namespace CampusBite.Infrastructure.Persistence;

// JJ-Sprint2 09/10/2026: datos (sin contraseña) de la base elegida, para mostrarlos al arrancar la API
public record DatabaseInfo(string Conexion, string Host, string Database, string Username)
{
    private bool EsHostLocal => Host is "localhost" or "127.0.0.1" or "::1";

    // Se considera producción si así se eligió o si el servidor no es la máquina local (p. ej. Neon)
    public bool EsProduccion => Conexion.Equals("Produccion", StringComparison.OrdinalIgnoreCase) || !EsHostLocal;
}
