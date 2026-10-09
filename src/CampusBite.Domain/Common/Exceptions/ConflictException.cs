namespace CampusBite.Domain.Common.Exceptions;

// JJ-Sprint2 09/10/2026: el recurso ya existe o choca con otro, p. ej. correo duplicado (la Api responde 409)
public class ConflictException : DomainException
{
    public ConflictException(string message) : base(message)
    {
    }
}
