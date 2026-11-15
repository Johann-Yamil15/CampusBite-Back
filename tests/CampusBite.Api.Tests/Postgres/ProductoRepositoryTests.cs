using CampusBite.Application.Productos;
using CampusBite.Domain.Common.Exceptions;
using CampusBite.Infrastructure.Productos;
using Npgsql;

namespace CampusBite.Api.Tests.Postgres;

// ProductoRepository contra los procedimientos reales de productos y categorías. Las pruebas que escriben crean sus
// propios productos y categorías (nombre único) en la cafetería semilla; los productos semilla solo se leen.
[Collection(ColeccionPostgres.Nombre)]
public class ProductoRepositoryTests(PostgresDePrueba bd)
{
    private static readonly Guid Cafeteria = PostgresDePrueba.Cafeteria;
    private static readonly Guid Encargado = PostgresDePrueba.Encargado;

    private ProductoRepository Repo() => new(bd.Contexto());

    private static string Unico(string prefijo) => $"{prefijo} {Guid.NewGuid().ToString("N")[..8]}";

    private async Task<Guid> CrearAsync(string? nombre = null, Guid? categoria = null, string? url = null,
        decimal precio = 42.50m, bool disponible = true, string? descripcion = "Hecho al momento")
    {
        var id = Guid.NewGuid();
        await Repo().CrearAsync(id, Cafeteria, Encargado,
            new DatosProducto(nombre ?? Unico("Torta"), descripcion, precio, categoria, disponible), url);
        return id;
    }

    [FactPostgres]
    public async Task Obtener_mapea_todos_los_campos_del_producto_semilla()
    {
        var p = await Repo().ObtenerAsync(Cafeteria, PostgresDePrueba.Chilaquiles, PostgresDePrueba.Alumno);

        Assert.NotNull(p);
        Assert.Equal(PostgresDePrueba.Chilaquiles, p.IdProducto);
        Assert.Equal(Cafeteria, p.IdCafeteria);
        Assert.Equal(PostgresDePrueba.Desayunos, p.IdCategoria);
        Assert.Equal("Desayunos", p.Categoria);
        Assert.Equal("Chilaquiles verdes", p.Nombre);
        Assert.Equal("Con pollo y crema", p.Descripcion);
        Assert.Equal(55.00m, p.Precio);
        Assert.Null(p.ImagenUrl);
        Assert.True(p.Disponible);
        Assert.NotEqual(default, p.CreadoEn);
    }

    [FactPostgres]
    public async Task Obtener_uno_que_no_existe_o_de_otra_cafeteria_devuelve_null()
    {
        Assert.Null(await Repo().ObtenerAsync(Cafeteria, Guid.NewGuid(), PostgresDePrueba.Alumno));
        Assert.Null(await Repo().ObtenerAsync(Guid.NewGuid(), PostgresDePrueba.Chilaquiles, PostgresDePrueba.Alumno));
    }

    [FactPostgres]
    public async Task Crear_guarda_cada_parametro_en_su_columna()
    {
        var nombre = Unico("Molletes");
        var id = await CrearAsync(nombre, PostgresDePrueba.Bebidas, "https://img.ejemplo.test/molletes.webp", 33.30m, true, "Con frijoles");

        var p = await Repo().ObtenerAsync(Cafeteria, id, Encargado);

        Assert.NotNull(p);
        Assert.Equal((id, nombre, "Con frijoles", 33.30m, PostgresDePrueba.Bebidas, "Bebidas"),
            (p.IdProducto, p.Nombre, p.Descripcion, p.Precio, p.IdCategoria, p.Categoria));
        Assert.Equal("https://img.ejemplo.test/molletes.webp", p.ImagenUrl);
        Assert.True(p.Disponible);
    }

    [FactPostgres]
    public async Task Crear_sin_categoria_ni_descripcion_ni_imagen_deja_NULL()
    {
        var id = await CrearAsync(categoria: null, url: null, descripcion: null);

        var p = await Repo().ObtenerAsync(Cafeteria, id, Encargado);

        Assert.NotNull(p);
        Assert.Null(p.IdCategoria);
        Assert.Null(p.Categoria);
        Assert.Null(p.Descripcion);
        Assert.Null(p.ImagenUrl);
    }

    [FactPostgres]
    public async Task Crear_repetido_da_Conflict_y_sin_permiso_Forbidden()
    {
        var nombre = Unico("Enchiladas");
        await CrearAsync(nombre);

        var repetido = await Assert.ThrowsAsync<ConflictException>(() => CrearAsync(nombre.ToUpperInvariant()));
        Assert.Contains("Ya existe un producto", repetido.Message);
        await Assert.ThrowsAsync<ForbiddenException>(() => Repo().CrearAsync(Guid.NewGuid(), Cafeteria,
            PostgresDePrueba.Alumno, new DatosProducto(Unico("Alumno"), null, 10m, null, true), null));
        await Assert.ThrowsAsync<NotFoundException>(() => Repo().CrearAsync(Guid.NewGuid(), Guid.NewGuid(),
            PostgresDePrueba.Admin, new DatosProducto(Unico("Sin cafeteria"), null, 10m, null, true), null));
    }

    [FactPostgres]
    public async Task Crear_con_precio_invalido_se_traduce_a_regla_de_negocio()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => CrearAsync(precio: 0m));
    }

    [FactPostgres]
    public async Task ActualizarImagen_devuelve_la_URL_anterior_y_valida_la_nueva()
    {
        var id = await CrearAsync();

        var primera = await Repo().ActualizarImagenAsync(Cafeteria, id, Encargado, "https://img.ejemplo.test/a.webp");
        var segunda = await Repo().ActualizarImagenAsync(Cafeteria, id, Encargado, "https://img.ejemplo.test/b.webp");

        Assert.Null(primera);
        Assert.Equal("https://img.ejemplo.test/a.webp", segunda);
        Assert.Equal("https://img.ejemplo.test/b.webp", (await Repo().ObtenerAsync(Cafeteria, id, Encargado))!.ImagenUrl);
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            Repo().ActualizarImagenAsync(Cafeteria, id, Encargado, "ftp://no-es-valida"));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Repo().ActualizarImagenAsync(Cafeteria, Guid.NewGuid(), Encargado, "https://img.ejemplo.test/c.webp"));
    }

    [FactPostgres]
    public async Task Actualizar_cambia_todos_los_campos_y_repetido_da_Conflict()
    {
        var id = await CrearAsync(categoria: PostgresDePrueba.Desayunos);
        var nuevo = Unico("Renombrado");

        await Repo().ActualizarAsync(id, Encargado, new DatosProducto(nuevo, "Nueva receta", 61.25m, null, false));

        var p = await Repo().ObtenerAsync(Cafeteria, id, Encargado);
        Assert.NotNull(p);
        Assert.Equal((nuevo, "Nueva receta", 61.25m, (Guid?)null, false),
            (p.Nombre, p.Descripcion, p.Precio, p.IdCategoria, p.Disponible));
        await Assert.ThrowsAsync<ConflictException>(() => Repo().ActualizarAsync(id, Encargado,
            new DatosProducto("Chilaquiles verdes", null, 10m, null, true)));
        await Assert.ThrowsAsync<NotFoundException>(() => Repo().ActualizarAsync(Guid.NewGuid(), Encargado,
            new DatosProducto(Unico("X"), null, 10m, null, true)));
    }

    [FactPostgres]
    public async Task ToggleDisponible_oculta_el_producto_al_alumno_pero_no_al_encargado()
    {
        var id = await CrearAsync();

        await Repo().ToggleDisponibleAsync(id, false, Encargado);

        Assert.Null(await Repo().ObtenerAsync(Cafeteria, id, PostgresDePrueba.Alumno));
        Assert.False((await Repo().ObtenerAsync(Cafeteria, id, Encargado))!.Disponible);
        await Repo().ToggleDisponibleAsync(id, true, Encargado);
        Assert.True((await Repo().ObtenerAsync(Cafeteria, id, PostgresDePrueba.Alumno))!.Disponible);
        await Assert.ThrowsAsync<ForbiddenException>(() => Repo().ToggleDisponibleAsync(id, false, PostgresDePrueba.Alumno));
        await Assert.ThrowsAsync<NotFoundException>(() => Repo().ToggleDisponibleAsync(Guid.NewGuid(), false, Encargado));
    }

    [FactPostgres]
    public async Task Eliminar_devuelve_la_imagen_para_borrarla_y_el_producto_desaparece()
    {
        var conImagen = await CrearAsync(url: "https://img.ejemplo.test/borrar.webp");
        var sinImagen = await CrearAsync();

        Assert.Equal("https://img.ejemplo.test/borrar.webp", await Repo().EliminarAsync(conImagen, Encargado));
        Assert.Null(await Repo().EliminarAsync(sinImagen, Encargado));
        Assert.Null(await Repo().ObtenerAsync(Cafeteria, conImagen, Encargado));
        await Assert.ThrowsAsync<NotFoundException>(() => Repo().EliminarAsync(conImagen, Encargado));
    }

    [FactPostgres]
    public async Task Eliminar_un_producto_con_historial_de_ordenes_da_Conflict_y_no_lo_borra()
    {
        var id = await CrearAsync();
        await bd.EjecutarAsync("""
            WITH o AS (
              INSERT INTO orden (id_usuario, id_cafeteria, recogida_en, estado, codigo_recogida)
              VALUES (@alumno, @cafeteria, now() + interval '1 day', 'entregada', '123456') RETURNING id_orden)
            INSERT INTO detalle_orden (id_orden, id_producto, cantidad, precio_unitario) SELECT id_orden, @p, 1, 42.50 FROM o
            """,
            new NpgsqlParameter("alumno", PostgresDePrueba.Alumno), new NpgsqlParameter("cafeteria", Cafeteria),
            new NpgsqlParameter("p", id));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => Repo().EliminarAsync(id, Encargado));

        Assert.Contains("desactívalo", ex.Message);
        Assert.NotNull(await Repo().ObtenerAsync(Cafeteria, id, Encargado));
    }

    [FactPostgres]
    public async Task Listar_pagina_busca_filtra_por_categoria_y_da_el_total()
    {
        var marca = Guid.NewGuid().ToString("N")[..8];
        var categoria = await Repo().CrearCategoriaAsync(Cafeteria, Encargado, $"Cat {marca}");
        for (var i = 1; i <= 3; i++)
        {
            await CrearAsync($"Lista {marca} {i}", categoria);
        }
        await CrearAsync($"Lista {marca} sin categoria");

        var (total, pagina) = await Repo().ListarAsync(Cafeteria, Encargado, marca, categoria, 2, 0);
        var (_, resto) = await Repo().ListarAsync(Cafeteria, Encargado, marca, categoria, 2, 2);
        var (todos, _) = await Repo().ListarAsync(Cafeteria, Encargado, marca, null, 20, 0);
        var (ninguno, vacio) = await Repo().ListarAsync(Cafeteria, Encargado, "no-existe-" + marca, null, 20, 0);

        Assert.Equal(3, total);
        Assert.Equal(2, pagina.Count);
        Assert.Single(resto);
        Assert.All(pagina.Concat(resto), p => Assert.Equal(categoria, p.IdCategoria));
        Assert.Equal(4, todos);
        Assert.Equal(0, ninguno);
        Assert.Empty(vacio);
    }

    [FactPostgres]
    public async Task Categorias_se_listan_crean_y_eliminan()
    {
        var nombre = Unico("Postres");

        var id = await Repo().CrearCategoriaAsync(Cafeteria, Encargado, nombre);
        var lista = await Repo().ListarCategoriasAsync(Cafeteria);

        Assert.Contains(new CampusBite.Application.Productos.Dtos.CategoriaDto(id, nombre), lista);
        Assert.Contains(new CampusBite.Application.Productos.Dtos.CategoriaDto(PostgresDePrueba.Desayunos, "Desayunos"), lista);
        await Assert.ThrowsAsync<ConflictException>(() => Repo().CrearCategoriaAsync(Cafeteria, Encargado, nombre.ToLowerInvariant()));
        await Assert.ThrowsAsync<ForbiddenException>(() => Repo().CrearCategoriaAsync(Cafeteria, PostgresDePrueba.Alumno, Unico("X")));

        await Repo().EliminarCategoriaAsync(id, Encargado);
        Assert.DoesNotContain(await Repo().ListarCategoriasAsync(Cafeteria), c => c.IdCategoria == id);
        await Assert.ThrowsAsync<NotFoundException>(() => Repo().EliminarCategoriaAsync(id, Encargado));
    }

    [FactPostgres]
    public async Task Las_categorias_de_una_cafeteria_sin_categorias_son_una_lista_vacia()
    {
        Assert.Empty(await Repo().ListarCategoriasAsync(Guid.NewGuid()));
    }
}
