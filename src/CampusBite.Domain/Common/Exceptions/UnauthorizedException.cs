namespace CampusBite.Domain.Common.Exceptions;

// JJ-Sprint2 09/10/2026: no se pudo comprobar la identidad, p. ej. credenciales incorrectas (la Api responde 401)
public class UnauthorizedException : DomainException
{
    public UnauthorizedException(string message) : base(message)
    {
    }
}
