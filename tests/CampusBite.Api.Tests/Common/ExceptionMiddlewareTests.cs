using System.Text.Json.Nodes;
using CampusBite.Api.Common.Middleware;
using CampusBite.Domain.Common.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CampusBite.Api.Tests.Common;

// ExceptionMiddleware directamente (sin servidor): la tabla excepción -> HTTP, el 503 con Retry-After, la cancelación
// del cliente y el 500 que no filtra detalles fuera de Development.
public class ExceptionMiddlewareTests
{
    // Una excepción de Domain que la tabla no conoce: debe caer en el 400 por defecto
    private sealed class ExcepcionNueva(string mensaje) : DomainException(mensaje);

    private sealed class LogDeMiddleware : ILogger<ExceptionMiddleware>
    {
        public List<(LogLevel Nivel, Exception? Excepcion)> Registros { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Registros.Add((logLevel, exception));
    }

    private static async Task<(DefaultHttpContext Contexto, JsonObject? Cuerpo)> Ejecutar(
        Exception error, string ambiente = "Production", CancellationToken abortado = default, ILogger<ExceptionMiddleware>? log = null)
    {
        var contexto = new DefaultHttpContext { RequestAborted = abortado };
        contexto.Request.Path = "/api/prueba";
        contexto.Response.Body = new MemoryStream();
        var middleware = new ExceptionMiddleware(_ => throw error, log ?? NullLogger<ExceptionMiddleware>.Instance,
            new HostingEnvironment { EnvironmentName = ambiente });

        await middleware.InvokeAsync(contexto);

        contexto.Response.Body.Position = 0;
        var texto = await new StreamReader(contexto.Response.Body).ReadToEndAsync();
        return (contexto, texto.Length == 0 ? null : JsonNode.Parse(texto)!.AsObject());
    }

    public static TheoryData<Exception, int> Tabla => new()
    {
        { new BusinessRuleException("regla"), 400 },
        { new UnauthorizedException("sesión"), 401 },
        { new ForbiddenException("permiso"), 403 },
        { new NotFoundException("no existe"), 404 },
        { new ConflictException("repetido"), 409 },
        { new ServiceUnavailableException("caído"), 503 },
        { new ExcepcionNueva("nueva"), 400 }
    };

    [Theory]
    [MemberData(nameof(Tabla))]
    public async Task Cada_excepcion_de_Domain_tiene_su_codigo_y_su_mensaje_en_ProblemDetails(Exception error, int codigo)
    {
        var (contexto, cuerpo) = await Ejecutar(error);

        Assert.Equal(codigo, contexto.Response.StatusCode);
        Assert.Equal("application/problem+json", contexto.Response.ContentType);
        Assert.Equal(error.Message, (string?)cuerpo!["detail"]);
        Assert.Equal(codigo, (int)cuerpo["status"]!);
        Assert.Equal(codigo == 503, contexto.Response.Headers.ContainsKey("Retry-After"));
    }

    [Fact]
    public async Task El_503_le_dice_al_cliente_cuando_reintentar()
    {
        var (contexto, _) = await Ejecutar(new ServiceUnavailableException("caído"));

        Assert.Equal("5", contexto.Response.Headers.RetryAfter.ToString());
    }

    [Fact]
    public async Task Si_el_cliente_cerro_la_conexion_no_se_escribe_nada()
    {
        using var cerrado = new CancellationTokenSource();
        await cerrado.CancelAsync();

        var (contexto, cuerpo) = await Ejecutar(new OperationCanceledException(cerrado.Token), abortado: cerrado.Token);

        Assert.Equal(200, contexto.Response.StatusCode);          // sin tocar
        Assert.Null(cuerpo);
    }

    [Fact]
    public async Task Una_cancelacion_que_no_vino_del_cliente_es_un_500()
    {
        var log = new LogDeMiddleware();

        var (contexto, _) = await Ejecutar(new OperationCanceledException("tiempo agotado interno"), log: log);

        Assert.Equal(500, contexto.Response.StatusCode);
        Assert.IsType<OperationCanceledException>(Assert.Single(log.Registros).Excepcion);
    }

    [Fact]
    public async Task Un_error_no_controlado_en_produccion_es_500_generico_y_queda_en_el_log()
    {
        var log = new LogDeMiddleware();

        var (contexto, cuerpo) = await Ejecutar(new InvalidOperationException("detalle interno con datos"), "Production", log: log);

        Assert.Equal(500, contexto.Response.StatusCode);
        Assert.Equal("Ocurrió un error interno en el servidor.", (string?)cuerpo!["detail"]);
        var registro = Assert.Single(log.Registros);
        Assert.Equal(LogLevel.Error, registro.Nivel);
        Assert.IsType<InvalidOperationException>(registro.Excepcion);
    }

    [Fact]
    public async Task En_desarrollo_el_500_muestra_el_mensaje_para_depurar()
    {
        var (_, cuerpo) = await Ejecutar(new InvalidOperationException("detalle interno"), Environments.Development);

        Assert.Equal("detalle interno", (string?)cuerpo!["detail"]);
    }
}
