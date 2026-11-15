using CampusBite.Application.Menu;
using CampusBite.Application.Menu.Dtos;
using CampusBite.Domain.Productos;

namespace CampusBite.Application.Tests.Menu;

// JJ-Sprint5 13/11/2026: búsqueda del menú (misma semántica que el filtro del front)
public class BuscadorMenuTests
{
    private static readonly List<MenuCafeteriaDto> Menu =
    [
        new()
        {
            IdCafeteria = Guid.NewGuid(), Cafeteria = "Cafetería Central", Abierta = true,
            Productos =
            [
                new() { IdProducto = Guid.NewGuid(), Producto = "Café americano", Categoria = "Bebidas", Precio = 25, MinutosPreparacion = 3 },
                new() { IdProducto = Guid.NewGuid(), Producto = "Ensalada", Categoria = "Comidas", Precio = 60, Popular = true,
                        Dietas = [Dieta.Vegetariano, Dieta.SinGluten], Calificacion = 4.5m },
                new() { IdProducto = Guid.NewGuid(), Producto = "Torta", Descripcion = "Con jamón", Categoria = "Comidas", Precio = 45 }
            ]
        },
        new()
        {
            IdCafeteria = Guid.NewGuid(), Cafeteria = "Sabor Universitario", Abierta = false,
            Productos = [new() { IdProducto = Guid.NewGuid(), Producto = "Café de olla", Categoria = "Bebidas", Precio = 20 }]
        }
    ];

    private static List<string> Nombres(BuscarMenuQuery q) =>
        BuscadorMenu.Buscar(Menu, q).Items.Select(p => p.Producto).ToList();

    [Fact]
    public void Texto_sin_acentos_ni_mayusculas_y_relevancia_por_nombre()
    {
        // "cafe" también coincide con "Cafetería Central": van primero los que lo tienen en el nombre
        Assert.Equal(["Café americano", "Café de olla"], Nombres(new BuscarMenuQuery { Texto = "CAFE" }).Take(2));
        Assert.Equal(["Café de olla"], Nombres(new BuscarMenuQuery { Texto = "OLLA" }));
        Assert.Equal(["Torta"], Nombres(new BuscarMenuQuery { Texto = "jamon" }));
        Assert.Equal(3, Nombres(new BuscarMenuQuery { Texto = "central" }).Count);
    }

    [Fact]
    public void Filtra_por_categoria_precio_dietas_y_abiertas()
    {
        Assert.Equal(2, Nombres(new BuscarMenuQuery { Categoria = "comidas" }).Count);
        Assert.Equal(["Café de olla"], Nombres(new BuscarMenuQuery { PrecioMax = 20 }));
        Assert.Equal(["Ensalada"], Nombres(new BuscarMenuQuery { Dietas = [Dieta.SinGluten] }));
        Assert.DoesNotContain("Café de olla", Nombres(new BuscarMenuQuery { SoloAbiertas = true }));
    }

    [Fact]
    public void Ordena_por_precio_calificacion_y_tiempo()
    {
        Assert.Equal("Café de olla", Nombres(new BuscarMenuQuery { Orden = OrdenBusqueda.PrecioAsc })[0]);
        Assert.Equal("Ensalada", Nombres(new BuscarMenuQuery { Orden = OrdenBusqueda.PrecioDesc })[0]);
        Assert.Equal("Ensalada", Nombres(new BuscarMenuQuery { Orden = OrdenBusqueda.Calificacion })[0]);
        Assert.Equal("Café americano", Nombres(new BuscarMenuQuery { Orden = OrdenBusqueda.Tiempo })[0]);
        Assert.Equal("Ensalada", Nombres(new BuscarMenuQuery())[0]);    // sin texto: populares primero
    }

    [Fact]
    public void Pagina_y_limita_el_tamano()
    {
        var pagina = BuscadorMenu.Buscar(Menu, new BuscarMenuQuery { Pagina = 2, TamanoPagina = 3 });
        Assert.Equal(4, pagina.Total);
        Assert.Single(pagina.Items);

        var limitada = BuscadorMenu.Buscar(Menu, new BuscarMenuQuery { Pagina = 0, TamanoPagina = 500 });
        Assert.Equal(1, limitada.Pagina);
        Assert.Equal(100, limitada.TamanoPagina);
    }
}
