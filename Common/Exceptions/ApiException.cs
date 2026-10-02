using System.Net;

namespace CampusBite_Back.Common.Exceptions;

// JJ-Sprint1 02/10/2026: excepción de negocio que el middleware traduce a un código HTTP
public class ApiException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public ApiException(string message, HttpStatusCode statusCode = HttpStatusCode.BadRequest)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public static ApiException NotFound(string message) => new(message, HttpStatusCode.NotFound);
    public static ApiException Conflict(string message) => new(message, HttpStatusCode.Conflict);
    public static ApiException Unauthorized(string message) => new(message, HttpStatusCode.Unauthorized);
}
