using CampusBite.Api.Common.Filters;
using CampusBite.Api.Common.Security;
using CampusBite.Application.Productos;
using CampusBite.Application.Productos.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using IUsuarioActual = CampusBite.Application.Common.Security.IUsuarioActual;

namespace CampusBite.Api.Productos;

// JY-Sprint4 27/10/2026: TEC-09/TEC-10. Productos de una cafetería. Sin lógica: recibe la petición, llama al servicio
// y responde. Permisos: la política es la primera barrera; la BD vuelve a revisar que el usuario sea encargado de
// ESA cafetería y que el producto sea suyo.
// Las dos acciones reciben imagen con las mismas reglas (JPEG, PNG o WebP, máx. 5 MB) y la misma tolerancia a mala
// señal: si la red falla, el cliente puede repetir la petición (503 trae Retry-After; 504 = tardó demasiado).
[ApiController]
[Route("api/cafeterias/{idCafeteria:guid}/productos")]
[Produces("application/json")]
[Authorize]
public class ProductosController : ControllerBase
{
    private readonly IProductoService _productos;
    private readonly IUsuarioActual _me;

    public ProductosController(IProductoService productos, IUsuarioActual me)
    {
        _productos = productos;
        _me = me;
    }

    // JJ-Sprint4 27/10/2026: TEC-09. Lista todos los productos (disponibles e inactivos) para el panel admin
    [HttpGet]
    [Authorize(Policy = PoliticasAutorizacion.PersonalCafeteria)]
    [ProducesResponseType<IReadOnlyList<ProductoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ProductoDto>>> Listar(Guid idCafeteria, CancellationToken cancellationToken)
        => Ok(await _productos.ListarAsync(idCafeteria, cancellationToken));

    // JJ-Sprint4 27/10/2026: TEC-09. Actualiza nombre, precio, categoría y disponibilidad. La imagen se gestiona por POST .../imagen
    [HttpPut("{idProducto:guid}")]
    [Authorize(Policy = PoliticasAutorizacion.PersonalCafeteria)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Actualizar(
        Guid idCafeteria, Guid idProducto, [FromBody] GuardarProductoDto dto, CancellationToken cancellationToken)
    {
        await _productos.ActualizarAsync(idProducto, dto, cancellationToken);
        return NoContent();
    }

    // JJ-Sprint4 27/10/2026: TEC-09. Toggle rápido de disponibilidad desde el panel admin
    [HttpPatch("{idProducto:guid}/disponible")]
    [Authorize(Policy = PoliticasAutorizacion.PersonalCafeteria)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleDisponible(
        Guid idCafeteria, Guid idProducto, [FromBody] bool disponible, CancellationToken cancellationToken)
    {
        await _productos.ToggleDisponibleAsync(idProducto, disponible, cancellationToken);
        return NoContent();
    }

    // JJ-Sprint4 27/10/2026: TEC-09. Elimina físicamente; 409 si tiene órdenes activas (desactivarlo primero)
    [HttpDelete("{idProducto:guid}")]
    [Authorize(Policy = PoliticasAutorizacion.PersonalCafeteria)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Eliminar(Guid idCafeteria, Guid idProducto, CancellationToken cancellationToken)
    {
        await _productos.EliminarAsync(idProducto, cancellationToken);
        return NoContent();
    }

    // Alta de un producto con imagen opcional (campo "archivo")
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
        // Sin Location: todavía no hay GET de un producto (lo agrega el resto de TEC-09)
        if (dto.Archivo is null)
        {
            return StatusCode(StatusCodes.Status201Created, await _productos.CrearAsync(idCafeteria, dto, null, cancellationToken));
        }

        await using var contenido = dto.Archivo.OpenReadStream();
        var imagen = new ArchivoImagen(contenido, dto.Archivo.ContentType, dto.Archivo.Length);
        return StatusCode(StatusCodes.Status201Created, await _productos.CrearAsync(idCafeteria, dto, imagen, cancellationToken));
    }

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
