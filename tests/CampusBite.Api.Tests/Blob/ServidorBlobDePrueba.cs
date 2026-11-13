using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CampusBite.Api.Tests.Blob;

// Servidor HTTP local (Kestrel, ya incluido en ASP.NET Core) que responde lo mínimo de la API REST de Blob Storage
// para que el SDK oficial de Azure funcione contra él: PUT de un blob (subida en una sola petición) y DELETE.
// No es Azure ni Azurite: no valida la firma Shared Key, solo registra lo que el SDK manda. Sirve para probar
// AzureBlobService sin red, sin cuenta y sin descargar nada. Modos para provocar fallos reales del SDK.
public sealed class ServidorBlobDePrueba : IAsyncDisposable
{
    public enum Modo
    {
        Acepta,
        Error500,
        Lento
    }

    public const string Cuenta = "cuentaprueba";
    public sealed record Peticion(string Metodo, string Ruta, IReadOnlyDictionary<string, string> Encabezados, byte[] Cuerpo);

    private readonly WebApplication _app;

    public ConcurrentQueue<Peticion> Peticiones { get; } = new();
    public ConcurrentDictionary<string, byte[]> Blobs { get; } = new();
    public string Base { get; }

    // Cadena de conexión con una llave aleatoria (no es secreta: el servidor de prueba no la revisa)
    public string CadenaDeConexion =>
        $"DefaultEndpointsProtocol=http;AccountName={Cuenta};" +
        $"AccountKey={Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))};BlobEndpoint={Base}/{Cuenta};";

    public ServidorBlobDePrueba(Modo modo = Modo.Acepta)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        _app = builder.Build();
        _app.Run(async contexto => await AtenderAsync(contexto, modo));
        _app.StartAsync().GetAwaiter().GetResult();
        Base = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
    }

    private async Task AtenderAsync(HttpContext contexto, Modo modo)
    {
        using var copia = new MemoryStream();
        await contexto.Request.Body.CopyToAsync(copia);
        var ruta = contexto.Request.Path.Value ?? string.Empty;
        Peticiones.Enqueue(new Peticion(contexto.Request.Method, ruta,
            contexto.Request.Headers.ToDictionary(h => h.Key.ToLowerInvariant(), h => h.Value.ToString()), copia.ToArray()));

        contexto.Response.Headers["x-ms-request-id"] = Guid.NewGuid().ToString();
        contexto.Response.Headers["x-ms-version"] = contexto.Request.Headers["x-ms-version"].ToString();
        if (modo == Modo.Lento)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), contexto.RequestAborted);
        }
        if (modo == Modo.Error500)
        {
            contexto.Response.StatusCode = StatusCodes.Status500InternalServerError;
            contexto.Response.Headers["x-ms-error-code"] = "InternalError";
            return;
        }

        switch (contexto.Request.Method)
        {
            case "PUT":
                Blobs[ruta] = copia.ToArray();
                contexto.Response.Headers.ETag = "\"0x8DC0000000000001\"";
                contexto.Response.Headers.LastModified = DateTimeOffset.UtcNow.ToString("R");
                contexto.Response.StatusCode = StatusCodes.Status201Created;
                break;
            case "DELETE" when Blobs.TryRemove(ruta, out _):
                contexto.Response.StatusCode = StatusCodes.Status202Accepted;
                break;
            case "DELETE":
                contexto.Response.StatusCode = StatusCodes.Status404NotFound;
                contexto.Response.Headers["x-ms-error-code"] = "BlobNotFound";
                break;
            default:
                contexto.Response.StatusCode = StatusCodes.Status400BadRequest;
                break;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
