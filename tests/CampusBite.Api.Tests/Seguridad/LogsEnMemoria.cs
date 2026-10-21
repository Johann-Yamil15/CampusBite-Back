using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace CampusBite.Api.Tests.Seguridad;

// JY-Sprint3 20/10/2026: guarda en memoria los logs de nivel Error para revisarlos en las pruebas
public sealed class LogsEnMemoria : ILoggerProvider, ILogger
{
    private readonly ConcurrentQueue<string> _errores = new();

    public IReadOnlyCollection<string> Errores => _errores;

    public ILogger CreateLogger(string categoryName) => this;
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel)) _errores.Enqueue($"{formatter(state, exception)} {exception?.Message}");
    }

    public void Dispose() { }
}
