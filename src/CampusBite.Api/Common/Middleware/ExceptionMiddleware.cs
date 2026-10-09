using CampusBite.Domain.Common.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace CampusBite.Api.Common.Middleware;

// JJ-Sprint2 09/10/2026: traduce las excepciones del dominio a su código HTTP y responde con un ProblemDetails uniforme
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException ex)
        {
            await WriteProblemAsync(context, ObtenerStatusCode(ex), ex.Message);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // El cliente cerró la conexión: no hay a quién responder
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error no controlado en {Path}", context.Request.Path);
            var detail = _env.IsDevelopment() ? ex.Message : "Ocurrió un error interno en el servidor.";
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, detail);
        }
    }

    // JJ-Sprint2 09/10/2026: única tabla de equivalencias dominio -> HTTP; una excepción nueva se agrega aquí
    private static int ObtenerStatusCode(DomainException ex) => ex switch
    {
        BusinessRuleException => StatusCodes.Status400BadRequest,
        UnauthorizedException => StatusCodes.Status401Unauthorized,
        ForbiddenException => StatusCodes.Status403Forbidden,
        NotFoundException => StatusCodes.Status404NotFound,
        ConflictException => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest
    };

    private static Task WriteProblemAsync(HttpContext context, int statusCode, string detail)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = ReasonPhrase(statusCode),
            Detail = detail,
            Instance = context.Request.Path
        };

        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
    }

    // Mantiene los mismos títulos que ya recibe el front (p. ej. "Conflict", "Unauthorized")
    private static string ReasonPhrase(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "BadRequest",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "NotFound",
        StatusCodes.Status409Conflict => "Conflict",
        _ => "InternalServerError"
    };
}
