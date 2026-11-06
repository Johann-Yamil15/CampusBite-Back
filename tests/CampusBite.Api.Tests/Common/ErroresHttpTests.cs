using System.Text.Json.Nodes;
using CampusBite.Api.Common.Middleware;
using CampusBite.Domain.Common.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CampusBite.Api.Tests.Common;

// JY-Sprint4 27/10/2026: TEC-10. ExceptionMiddleware y ProblemaHttp probados directo: código, título, Retry-After y el
// 500 sin detalles internos. Agregado tras las pruebas de mutación con Stryker.
public class ErroresHttpTests
{
    private static async Task<(HttpContext Http, JsonObject Cuerpo)> Ejecutar(Exception error, string ambiente = "Production")
    {
        var http = new DefaultHttpContext();
        http.Request.Path = "/api/prueba";
        http.Response.Body = new MemoryStream();
        var middleware = new ExceptionMiddleware(_ => throw error, NullLogger<ExceptionMiddleware>.Instance, new Ambiente(ambiente));

        await middleware.InvokeAsync(http);

        http.Response.Body.Position = 0;
        return (http, JsonNode.Parse(http.Response.Body)!.AsObject());
    }

    [Theory]
    [InlineData(typeof(BusinessRuleException), 400, "BadRequest")]
    [InlineData(typeof(UnauthorizedException), 401, "Unauthorized")]
    [InlineData(typeof(ForbiddenException), 403, "Forbidden")]
    [InlineData(typeof(NotFoundException), 404, "NotFound")]
    [InlineData(typeof(ConflictException), 409, "Conflict")]
    [InlineData(typeof(ServiceUnavailableException), 503, "ServiceUnavailable")]
    public async Task Cada_error_de_dominio_da_su_codigo_titulo_y_mensaje(Type tipo, int codigo, string titulo)
    {
        var (http, cuerpo) = await Ejecutar((Exception)Activator.CreateInstance(tipo, "Mensaje para el usuario")!);

        Assert.Equal(codigo, http.Response.StatusCode);
        Assert.Equal("application/problem+json", http.Response.ContentType);
        Assert.Equal(titulo, cuerpo["title"]!.GetValue<string>());
        Assert.Equal("Mensaje para el usuario", cuerpo["detail"]!.GetValue<string>());
        Assert.Equal("/api/prueba", cuerpo["instance"]!.GetValue<string>());
    }

    [Fact]
    public async Task Solo_el_503_trae_Retry_After()
    {
        var (con, _) = await Ejecutar(new ServiceUnavailableException("x"));
        var (sin, _) = await Ejecutar(new ConflictException("x"));

        Assert.Equal("5", con.Response.Headers.RetryAfter.ToString());
        Assert.False(sin.Response.Headers.ContainsKey("Retry-After"));
    }

    [Fact]
    public async Task Un_error_no_controlado_en_produccion_es_500_sin_detalles_internos()
    {
        var (http, cuerpo) = await Ejecutar(new InvalidOperationException("cadena de conexión secreta"));

        Assert.Equal(500, http.Response.StatusCode);
        Assert.Equal("InternalServerError", cuerpo["title"]!.GetValue<string>());
        Assert.Equal("Ocurrió un error interno en el servidor.", cuerpo["detail"]!.GetValue<string>());
    }

    [Fact]
    public async Task Un_error_no_controlado_queda_en_el_log_con_la_ruta()
    {
        var log = new LogFalso();
        var http = new DefaultHttpContext { Request = { Path = "/api/prueba" }, Response = { Body = new MemoryStream() } };
        var error = new InvalidOperationException("falla");

        await new ExceptionMiddleware(_ => throw error, log, new Ambiente("Production")).InvokeAsync(http);

        var (nivel, mensaje, excepcion) = Assert.Single(log.Registros);
        Assert.Equal(LogLevel.Error, nivel);
        Assert.Equal("Error no controlado en /api/prueba", mensaje);
        Assert.Same(error, excepcion);
    }

    [Fact]
    public async Task En_desarrollo_el_500_muestra_el_mensaje_para_depurar()
    {
        var (_, cuerpo) = await Ejecutar(new InvalidOperationException("detalle para depurar"), "Development");

        Assert.Equal("detalle para depurar", cuerpo["detail"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(413, "PayloadTooLarge")]
    [InlineData(504, "GatewayTimeout")]
    public async Task ProblemaHttp_usa_el_titulo_de_cada_codigo(int codigo, string titulo)
    {
        var http = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        await ProblemaHttp.EscribirAsync(http, codigo, "detalle");

        http.Response.Body.Position = 0;
        Assert.Equal(codigo, http.Response.StatusCode);
        Assert.Equal(titulo, JsonNode.Parse(http.Response.Body)!["title"]!.GetValue<string>());
    }

    private sealed class LogFalso : ILogger<ExceptionMiddleware>
    {
        public List<(LogLevel Nivel, string Mensaje, Exception? Excepcion)> Registros { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Registros.Add((logLevel, formatter(state, exception), exception));
    }

    private sealed class Ambiente(string nombre) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = nombre;
        public string ApplicationName { get; set; } = "CampusBite";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
