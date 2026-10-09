namespace CampusBite.Domain.Common.Exceptions;

// JJ-Sprint2 09/10/2026: el usuario está identificado pero su rol no le permite la acción (la Api responde 403)
public class ForbiddenException : DomainException
{
    public ForbiddenException(string message) : base(message)
    {
    }
}
