using Microsoft.AspNetCore.Mvc;

namespace CampusBite.Api.Common.Middleware;

// JY-Sprint2 10/10/2026: un solo formato de error para toda la API (ProblemDetails). Lo usan ExceptionMiddleware y
// las respuestas 401/403 de la autenticación, para que el front siempre lea "detail" igual.
public static class ProblemaHttp
{
    public static Task EscribirAsync(HttpContext context, int statusCode, string detail)
    {
        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsJsonAsync(Crear(context, statusCode, detail), options: null, contentType: "application/problem+json");
    }

    // JY-Sprint4 27/10/2026: TEC-10, para los filtros de MVC que responden con un IActionResult en vez de escribir
    public static ObjectResult Resultado(HttpContext context, int statusCode, string detail) =>
        new(Crear(context, statusCode, detail)) { StatusCode = statusCode, ContentTypes = { "application/problem+json" } };

    private static ProblemDetails Crear(HttpContext context, int statusCode, string detail) => new()
    {
        Status = statusCode,
        Title = ReasonPhrase(statusCode),
        Detail = detail,
        Instance = context.Request.Path
    };

    // Mantiene los mismos títulos que ya recibe el front (p. ej. "Conflict", "Unauthorized")
    private static string ReasonPhrase(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "BadRequest",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "NotFound",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status413PayloadTooLarge => "PayloadTooLarge",
        StatusCodes.Status503ServiceUnavailable => "ServiceUnavailable",
        StatusCodes.Status504GatewayTimeout => "GatewayTimeout",
        _ => "InternalServerError"
    };
}
