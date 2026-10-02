using System.Net;
using CampusBite_Back.Common.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace CampusBite_Back.Common.Middleware;

// JJ-Sprint1 02/10/2026: captura excepciones y responde con un ProblemDetails uniforme
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
        catch (ApiException ex)
        {
            await WriteProblemAsync(context, ex.StatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error no controlado en {Path}", context.Request.Path);
            var detail = _env.IsDevelopment() ? ex.Message : "Ocurrió un error interno en el servidor.";
            await WriteProblemAsync(context, HttpStatusCode.InternalServerError, detail);
        }
    }

    private static Task WriteProblemAsync(HttpContext context, HttpStatusCode statusCode, string detail)
    {
        var problem = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = statusCode.ToString(),
            Detail = detail,
            Instance = context.Request.Path
        };

        context.Response.StatusCode = (int)statusCode;
        return context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
    }
}
