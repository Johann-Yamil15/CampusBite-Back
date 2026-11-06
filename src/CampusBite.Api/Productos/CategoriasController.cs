using CampusBite.Api.Common.Security;
using CampusBite.Application.Productos;
using CampusBite.Application.Productos.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CampusBite.Api.Productos;

// JJ-Sprint4 27/10/2026: TEC-09. Categorías de una cafetería. GET es público para cualquier usuario con sesión
// (poblar selectores en el formulario de orden); POST y DELETE son solo para PersonalCafeteria.
[ApiController]
[Route("api/cafeterias/{idCafeteria:guid}/categorias")]
[Produces("application/json")]
[Authorize]
public class CategoriasController : ControllerBase
{
    private readonly IProductoService _productos;

    public CategoriasController(IProductoService productos)
    {
        _productos = productos;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CategoriaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<CategoriaDto>>> Listar(Guid idCafeteria, CancellationToken cancellationToken)
        => Ok(await _productos.ListarCategoriasAsync(idCafeteria, cancellationToken));

    [HttpPost]
    [Authorize(Policy = PoliticasAutorizacion.PersonalCafeteria)]
    [ProducesResponseType<CategoriaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoriaDto>> Crear(
        Guid idCafeteria, [FromBody] CrearCategoriaDto dto, CancellationToken cancellationToken)
    {
        var id = await _productos.CrearCategoriaAsync(idCafeteria, dto.Nombre, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, new CategoriaDto(id, dto.Nombre.Trim()));
    }

    [HttpDelete("{idCategoria:guid}")]
    [Authorize(Policy = PoliticasAutorizacion.PersonalCafeteria)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(Guid idCafeteria, Guid idCategoria, CancellationToken cancellationToken)
    {
        await _productos.EliminarCategoriaAsync(idCategoria, cancellationToken);
        return NoContent();
    }
}
