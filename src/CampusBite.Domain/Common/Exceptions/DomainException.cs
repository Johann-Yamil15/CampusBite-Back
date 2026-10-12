namespace CampusBite.Domain.Common.Exceptions;

// JJ-Sprint2 09/10/2026: base de los errores de negocio. No conoce HTTP: la capa Api decide el código de respuesta
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }
}
