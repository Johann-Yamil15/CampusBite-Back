namespace CampusBite.Domain.Common.Exceptions;

// JY-Sprint4 27/10/2026: TEC-10. Un servicio externo (p. ej. Azure Blob Storage) no respondió después de los
// reintentos. No es culpa del usuario: puede repetir la misma petición en unos segundos (la Api responde 503)
public class ServiceUnavailableException : DomainException
{
    public ServiceUnavailableException(string message) : base(message)
    {
    }
}
