namespace CampusBite.Domain.Common.Exceptions;

// JJ-Sprint2 09/10/2026: el recurso solicitado no existe (la Api responde 404)
public class NotFoundException : DomainException
{
    public NotFoundException(string message) : base(message)
    {
    }
}
