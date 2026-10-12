namespace CampusBite.Domain.Common.Exceptions;

// JJ-Sprint2 09/10/2026: los datos no cumplen una regla del negocio (la Api responde 400)
public class BusinessRuleException : DomainException
{
    public BusinessRuleException(string message) : base(message)
    {
    }
}
