using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace CampusBite.Api.Tests.Common;

// Proveedor de logs para las pruebas: guarda nivel, categoría, mensaje y excepción de todo lo que registra la API real
public sealed class LogEnMemoria : ILoggerProvider
{
    public sealed record Registro(LogLevel Nivel, string Categoria, string Mensaje, Exception? Excepcion);

    public ConcurrentQueue<Registro> Registros { get; } = new();

    public IEnumerable<Registro> De<T>() => Registros.Where(r => r.Categoria == typeof(T).FullName);

    public ILogger CreateLogger(string categoryName) => new Logger(categoryName, Registros);

    public void Dispose()
    {
    }

    private sealed class Logger(string categoria, ConcurrentQueue<Registro> destino) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            destino.Enqueue(new Registro(logLevel, categoria, formatter(state, exception), exception));
    }
}
