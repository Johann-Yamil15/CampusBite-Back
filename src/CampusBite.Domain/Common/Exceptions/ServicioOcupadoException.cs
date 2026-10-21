namespace CampusBite.Domain.Common.Exceptions;

// JY-Sprint3 20/10/2026: la operación no se pudo hacer AHORA, pero reintentarla en unos segundos puede funcionar
// (base saturada, un bloqueo que tardó demasiado, una consulta que excedió su tiempo). La Api responde 503 con
// Retry-After, para que el front reintente en lugar de mostrar un error definitivo.
public class ServicioOcupadoException : DomainException
{
    public ServicioOcupadoException(string message) : base(message)
    {
    }
}
