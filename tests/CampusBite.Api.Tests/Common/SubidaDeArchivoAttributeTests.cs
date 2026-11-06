using CampusBite.Api.Common.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Core.Features;

namespace CampusBite.Api.Tests.Common;

// JY-Sprint4 27/10/2026: TEC-10. SubidaDeArchivoAttribute probado directo (límites exactos y features de Kestrel que
// TestServer no tiene). Agregado tras las pruebas de mutación con Stryker.
public class SubidaDeArchivoAttributeTests
{
    private const long Maximo = 1000;
    private const long Limite = Maximo + 64 * 1024;   // máximo + margen del multipart

    private readonly TamanoFalso _tamano = new();
    private readonly TasaFalsa _tasa = new();

    private ResourceExecutingContext Contexto(long? contentLength, string protocolo = "HTTP/1.1")
    {
        var http = new DefaultHttpContext();
        http.Request.ContentLength = contentLength;
        http.Request.Protocol = protocolo;
        http.Features.Set<IHttpMaxRequestBodySizeFeature>(_tamano);
        http.Features.Set<IHttpMinRequestBodyDataRateFeature>(_tasa);
        return new ResourceExecutingContext(
            new ActionContext(http, new RouteData(), new ActionDescriptor()), [], new List<IValueProviderFactory>());
    }

    [Theory]
    [InlineData(null)]          // envío por partes: sin Content-Length
    [InlineData(Limite)]        // justo en el límite
    public void Dentro_del_limite_deja_pasar_y_fija_el_tamano_maximo_del_cuerpo(long? contentLength)
    {
        var contexto = Contexto(contentLength);

        new SubidaDeArchivoAttribute(Maximo).OnResourceExecuting(contexto);

        Assert.Null(contexto.Result);
        Assert.Equal(Limite, _tamano.MaxRequestBodySize);
    }

    [Fact]
    public void Un_byte_sobre_el_limite_es_413_y_no_toca_nada_mas()
    {
        var contexto = Contexto(Limite + 1);

        new SubidaDeArchivoAttribute(Maximo).OnResourceExecuting(contexto);

        var resultado = Assert.IsType<ObjectResult>(contexto.Result);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, resultado.StatusCode);
        Assert.Contains("application/problem+json", resultado.ContentTypes);
        Assert.Null(_tamano.MaxRequestBodySize);
        Assert.Null(_tasa.MinDataRate);
    }

    [Theory]
    [InlineData("HTTP/1.1")]
    [InlineData("HTTP/1.0")]
    public void En_HTTP_1_tolera_señal_lenta(string protocolo)
    {
        new SubidaDeArchivoAttribute(Maximo).OnResourceExecuting(Contexto(100, protocolo));

        Assert.Equal(64, _tasa.MinDataRate!.BytesPerSecond);
        Assert.Equal(TimeSpan.FromSeconds(30), _tasa.MinDataRate.GracePeriod);
    }

    [Fact]
    public void En_HTTP_2_no_cambia_la_tasa_porque_Kestrel_no_lo_permite()
    {
        new SubidaDeArchivoAttribute(Maximo).OnResourceExecuting(Contexto(100, "HTTP/2"));

        Assert.Null(_tasa.MinDataRate);
    }

    [Fact]
    public void Si_el_tamano_ya_no_se_puede_cambiar_no_falla()
    {
        _tamano.IsReadOnly = true;

        new SubidaDeArchivoAttribute(Maximo).OnResourceExecuting(Contexto(100));

        Assert.Null(_tamano.MaxRequestBodySize);
    }

    private sealed class TamanoFalso : IHttpMaxRequestBodySizeFeature
    {
        public bool IsReadOnly { get; set; }
        public long? MaxRequestBodySize { get; set; }
    }

    private sealed class TasaFalsa : IHttpMinRequestBodyDataRateFeature
    {
        public MinDataRate? MinDataRate { get; set; }
    }
}
