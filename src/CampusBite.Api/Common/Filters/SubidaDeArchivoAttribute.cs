using CampusBite.Api.Common.Middleware;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Core.Features;

namespace CampusBite.Api.Common.Filters;

// JY-Sprint4 27/10/2026: TEC-10. Prepara una acción que recibe un archivo desde el celular. Corre antes de leer el
// cuerpo (es un filtro de recurso, previo al model binding):
//  - Tamaño: si Content-Length ya pasa del máximo responde 413 sin esperar a recibir el archivo; si no viene
//    Content-Length (envío por partes), Kestrel corta al llegar al límite.
//  - Señal débil: Kestrel cancela por defecto una subida que va a menos de 240 bytes/s pasados 5 s. Aquí se baja a
//    64 bytes/s con 30 s de gracia para que una pausa de la red (túnel, cambio de antena) no la mate.
// El tiempo total lo limita la política PoliticaTiempo (AddSubidaDeArchivos): pasado ese tiempo responde 504 y el
// cliente puede reintentar; repetir la subida es seguro (ProductoService).
[AttributeUsage(AttributeTargets.Method)]
public sealed class SubidaDeArchivoAttribute : Attribute, IResourceFilter
{
    public const string PoliticaTiempo = "SubidaArchivo";
    public static readonly TimeSpan TiempoMaximo = TimeSpan.FromMinutes(3);   // menor a los 230 s en que corta Azure

    // Lo que ocupa el multipart además del archivo (separadores y encabezados de cada parte)
    private const long MargenMultipart = 64 * 1024;
    private static readonly MinDataRate TasaMinima = new(bytesPerSecond: 64, gracePeriod: TimeSpan.FromSeconds(30));

    private readonly long _tamanoMaximoArchivo;

    public SubidaDeArchivoAttribute(long tamanoMaximoArchivo)
    {
        _tamanoMaximoArchivo = tamanoMaximoArchivo;
    }

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var http = context.HttpContext;
        var limite = _tamanoMaximoArchivo + MargenMultipart;

        if (http.Request.ContentLength > limite)
        {
            context.Result = ProblemaHttp.Resultado(http, StatusCodes.Status413PayloadTooLarge,
                $"La imagen pesa más de {_tamanoMaximoArchivo / (1024 * 1024)} MB. Redúcela e intenta de nuevo.");
            return;
        }

        if (http.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } tamano)
        {
            tamano.MaxRequestBodySize = limite;
        }

        // Solo HTTP/1.x: en HTTP/2 Kestrel no permite cambiarla (lanza NotSupportedException)
        if (HttpProtocol.IsHttp11(http.Request.Protocol) || HttpProtocol.IsHttp10(http.Request.Protocol))
        {
            if (http.Features.Get<IHttpMinRequestBodyDataRateFeature>() is { } tasa)
            {
                tasa.MinDataRate = TasaMinima;
            }
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
        // Nada que hacer después
    }
}
