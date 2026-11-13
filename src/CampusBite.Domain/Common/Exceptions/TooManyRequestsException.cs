namespace CampusBite.Domain.Common.Exceptions;

// JJ-Sprint5 13/11/2026: demasiados intentos (p. ej. códigos de recogida erróneos); la Api responde 429
public class TooManyRequestsException : DomainException
{
    public TooManyRequestsException(string message) : base(message)
    {
    }
}
