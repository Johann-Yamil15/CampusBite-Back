using CampusBite.Api.Common.Filters;
using CampusBite.Api.Common.Security;
using CampusBite.Application.Productos;
using CampusBite.Application.Productos.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;

namespace CampusBite.Api.Productos;

// JY-Sprint4 27/10/2026: TEC-09/TEC-10. Productos de una cafetería. Sin lógica: recibe la petición, llama al servicio
// y responde. Permisos: la política es la primera barrera; la BD vuelve a revisar que el usuario sea encargado de
// ESA cafetería y que el producto sea suyo.
// Las dos acciones que reciben imagen lo hacen con las mismas reglas (JPEG, PNG o WebP, máx. 5 MB) y la misma tolerancia a mala
// señal: si la red falla, el cliente puede repetir la petición (503 trae Retry-After; 504 = tardó demasiado).
[ApiController]
[Route("api/cafeterias/{idCafeteria:guid}/productos")]
[Produces("application/json")]
[Authorize]
public class ProductosController : ControllerBase
{
    private const string RutaObtener = "ObtenerProducto";

    private readonly IProductoService _productos;

    public ProductosController(IProductoService productos)
    {
        _productos = productos;
    }

    // Cualquier usuario con sesión ve un producto disponible de una cafetería activa; uno no disponible (o de una
    // cafetería inactiva) solo lo ven su encargado y el admin del sistema. Para los demás es 404
    // Los nombres de ruta son globales: "Obtener" ya es el de cafeterías
    [HttpGet("{idProducto:guid}", Name = RutaObtener)]
    [ProducesResponseType<ProductoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductoDto>> Obtener(Guid idCafeteria, Guid idProducto, CancellationToken cancellationToken)
    {
        return Ok(await _productos.ObtenerAsync(idCafeteria, idProducto, cancellationToken));
    }

    // Alta de un producto con imagen opcional (campo "archivo"). 201 con Location al GET del producto
    [HttpPost]
    [Authorize(Policy = PoliticasAutorizacion.PersonalCafeteria)]
    [Consumes("multipart/form-data")]
    [SubidaDeArchivo(ReglasImagenProducto.TamanoMaximoBytes)]
    [RequestTimeout(SubidaDeArchivoAttribute.PoliticaTiempo)]
    [ProducesResponseType<ProductoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<ProductoDto>> Crear(
        Guid idCafeteria, [FromForm] CrearProductoFormDto dto, CancellationToken cancellationToken)
    {
        if (dto.Archivo is null)
        {
            return Creado(await _productos.CrearAsync(idCafeteria, dto, null, cancellationToken));
        }

        await using var contenido = dto.Archivo.OpenReadStream();
        var imagen = new ArchivoImagen(contenido, dto.Archivo.ContentType, dto.Archivo.Length);
        return Creado(await _productos.CrearAsync(idCafeteria, dto, imagen, cancellationToken));
    }

    private CreatedAtRouteResult Creado(ProductoDto creado) =>
        CreatedAtRoute(RutaObtener, new { idCafeteria = creado.IdCafeteria, idProducto = creado.IdProducto }, creado);

    // Cambia la imagen: la guarda en producto.imagen_url y borra la anterior
    [HttpPost("{idProducto:guid}/imagen")]
    [Authorize(Policy = PoliticasAutorizacion.PersonalCafeteria)]
    [Consumes("multipart/form-data")]
    [SubidaDeArchivo(ReglasImagenProducto.TamanoMaximoBytes)]
    [RequestTimeout(SubidaDeArchivoAttribute.PoliticaTiempo)]
    [ProducesResponseType<ImagenProductoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status504GatewayTimeout)]
    public async Task<ActionResult<ImagenProductoDto>> SubirImagen(
        Guid idCafeteria, Guid idProducto, [FromForm] SubirImagenProductoDto dto, CancellationToken cancellationToken)
    {
        var archivo = dto.Archivo!;
        await using var contenido = archivo.OpenReadStream();

        return Ok(await _productos.SubirImagenAsync(
            idCafeteria, idProducto, new ArchivoImagen(contenido, archivo.ContentType, archivo.Length), cancellationToken));
    }
}
