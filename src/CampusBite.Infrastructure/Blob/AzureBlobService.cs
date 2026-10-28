using System.Globalization;
using Azure;
using Azure.Core;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CampusBite.Application.Productos;
using CampusBite.Domain.Common.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CampusBite.Infrastructure.Blob;

// JY-Sprint4 27/10/2026: TEC-10. Implementa IBlobService con Azure Blob Storage (contenedor público de lectura).
//  - Nombre del blob: {id_producto}_{fecha UTC con milisegundos}.{ext}. Cada subida es un archivo nuevo, así la URL
//    es permanente y el navegador o CDN nunca muestran una imagen vieja en caché.
//  - Reintentos exponenciales del SDK (AzureBlobSettings). La imagen se manda en bloques de 1 MB: si la red se
//    corta a la mitad, solo se repite el bloque que falló, no los 5 MB.
//  - Cualquier fallo de Azure, ya agotados los reintentos, sale como ServiceUnavailableException (503) con un
//    mensaje genérico; el detalle técnico queda solo en el log.
internal sealed class AzureBlobService : IBlobService
{
    private const string MensajeNoDisponible =
        "El servicio de imágenes no está disponible en este momento. Intenta de nuevo en unos segundos.";
    private const int TamanoBloque = 1024 * 1024;

    private readonly Lazy<BlobContainerClient> _contenedor;
    private readonly ILogger<AzureBlobService> _logger;

    public AzureBlobService(IOptions<AzureBlobSettings> opciones, ILogger<AzureBlobService> logger)
    {
        _logger = logger;
        // Se crea al primer uso: la API arranca aunque Azure no esté configurado (p. ej. en local o en pruebas).
        // PublicationOnly no guarda la excepción: cada petición vuelve a revisar y deja el motivo en el log.
        _contenedor = new Lazy<BlobContainerClient>(() => CrearContenedor(opciones.Value), LazyThreadSafetyMode.PublicationOnly);
    }

    public async Task<string> SubirImagenProductoAsync(
        Guid idProducto, Stream contenido, string contentType, CancellationToken ct = default)
    {
        var fecha = DateTimeOffset.UtcNow.ToString("yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture);
        var blob = _contenedor.Value.GetBlobClient($"{idProducto}_{fecha}.{Extension(contentType)}");

        try
        {
            await blob.UploadAsync(contenido, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType,
                    // El nombre nunca se reutiliza: el navegador puede guardarla en caché un año
                    CacheControl = "public, max-age=31536000, immutable"
                },
                TransferOptions = new StorageTransferOptions
                {
                    InitialTransferSize = TamanoBloque,
                    MaximumTransferSize = TamanoBloque,
                    MaximumConcurrency = 2
                }
            }, ct);
        }
        catch (Exception ex) when (EsFalloDeAzure(ex, ct))
        {
            _logger.LogError(ex, "No se pudo subir la imagen del producto {IdProducto} a Azure Blob Storage", idProducto);
            throw new ServiceUnavailableException(MensajeNoDisponible);
        }

        return blob.Uri.ToString();
    }

    public async Task EliminarImagenAsync(string urlBlob, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(urlBlob, UriKind.Absolute, out var uri))
        {
            return;
        }

        // Solo se borran blobs de este contenedor: una URL de otro sitio (p. ej. datos cargados a mano) no se toca
        var contenedor = _contenedor.Value;
        var prefijo = contenedor.Uri.GetLeftPart(UriPartial.Path).TrimEnd('/') + "/";
        var ruta = uri.GetLeftPart(UriPartial.Path);
        if (!ruta.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase) || ruta.Length == prefijo.Length)
        {
            return;
        }

        var nombre = Uri.UnescapeDataString(ruta[prefijo.Length..]);
        try
        {
            await contenedor.GetBlobClient(nombre).DeleteIfExistsAsync(cancellationToken: ct);
        }
        catch (Exception ex) when (EsFalloDeAzure(ex, ct))
        {
            _logger.LogWarning(ex, "No se pudo borrar el blob {Blob}; queda huérfano en Azure Blob Storage", nombre);
            throw new ServiceUnavailableException(MensajeNoDisponible);
        }
    }

    private BlobContainerClient CrearContenedor(AzureBlobSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            _logger.LogError("AzureBlob:ConnectionString no está configurada. Define AzureBlob__ConnectionString en el " +
                             "archivo .env o en las variables de entorno para poder subir imágenes.");
            throw new ServiceUnavailableException(MensajeNoDisponible);
        }

        var opciones = new BlobClientOptions
        {
            Retry =
            {
                Mode = RetryMode.Exponential,
                MaxRetries = settings.MaxReintentos,
                Delay = TimeSpan.FromSeconds(settings.EsperaInicialSegundos),
                MaxDelay = TimeSpan.FromSeconds(settings.EsperaMaximaSegundos),
                NetworkTimeout = TimeSpan.FromSeconds(settings.TiempoPorIntentoSegundos)
            }
        };

        return new BlobServiceClient(settings.ConnectionString, opciones)
            .GetBlobContainerClient(settings.ContainerProductos);
    }

    // ReglasImagenProducto ya garantiza que solo llegan estos tres tipos
    private static string Extension(string contentType) => contentType switch
    {
        "image/webp" => "webp",
        "image/png" => "png",
        _ => "jpg"
    };

    // Lo que lanza el SDK cuando Azure responde con error (RequestFailedException), cuando se agotan los reintentos
    // por errores de red (AggregateException), cuando la conexión se cae (IOException) o cuando un intento pasa su
    // tiempo límite (OperationCanceledException sin que el cliente haya cancelado). Si canceló el cliente, sube tal cual.
    private static bool EsFalloDeAzure(Exception ex, CancellationToken ct) => ex switch
    {
        RequestFailedException or AggregateException or IOException => true,
        OperationCanceledException => !ct.IsCancellationRequested,
        _ => false
    };
}
