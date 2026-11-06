using System.ComponentModel.DataAnnotations;
using CampusBite.Application.Productos.Dtos;

namespace CampusBite.Application.Tests.Productos;

// JY-Sprint4 27/10/2026: TEC-09. Validaciones de GuardarProductoDto (las mismas que sp_validar_datos_producto)
public class GuardarProductoDtoTests
{
    private static List<string> Errores(GuardarProductoDto dto)
    {
        var resultados = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), resultados, validateAllProperties: true);
        return resultados.SelectMany(r => r.MemberNames).ToList();
    }

    private static GuardarProductoDto Valido() => new() { Nombre = "Café americano", Precio = 25m };

    [Fact]
    public void Un_producto_valido_no_tiene_errores()
    {
        Assert.Empty(Errores(Valido()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" A ")]
    public void Nombre_de_menos_de_2_caracteres_es_invalido(string nombre)
    {
        var dto = Valido();
        dto.Nombre = nombre;

        Assert.Contains(nameof(GuardarProductoDto.Nombre), Errores(dto));
    }

    [Fact]
    public void Nombre_de_mas_de_100_y_descripcion_de_mas_de_500_son_invalidos()
    {
        var dto = Valido();
        dto.Nombre = new string('x', 101);
        dto.Descripcion = new string('x', 501);

        Assert.Equal([nameof(GuardarProductoDto.Descripcion), nameof(GuardarProductoDto.Nombre)], Errores(dto).Order());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1000000")]
    [InlineData("10.555")]
    public void Precio_faltante_fuera_de_rango_o_con_mas_de_2_decimales_es_invalido(string? precio)
    {
        var dto = Valido();
        dto.Precio = precio is null ? null : decimal.Parse(precio, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Contains(nameof(GuardarProductoDto.Precio), Errores(dto));
    }

    [Theory]
    [InlineData("0.01")]
    [InlineData("55.5")]
    [InlineData("999999.99")]
    public void Precio_en_rango_es_valido(string precio)
    {
        var dto = Valido();
        dto.Precio = decimal.Parse(precio, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Empty(Errores(dto));
    }

    [Fact]
    public void Un_dto_nuevo_esta_disponible_y_sin_nombre()
    {
        var dto = new GuardarProductoDto { Precio = 10m };

        Assert.True(dto.Disponible);
        Assert.Contains(nameof(GuardarProductoDto.Nombre), Errores(dto));
    }

    [Fact]
    public void Nombre_de_exactamente_2_caracteres_es_valido()
    {
        var dto = Valido();
        dto.Nombre = " Té ";

        Assert.Empty(Errores(dto));
    }

    // Los mensajes llegan tal cual al front en "errors"
    [Theory]
    [InlineData(" A ", "25", null, "El nombre debe tener al menos 2 caracteres.")]
    [InlineData("Café", "0", null, "El precio debe ser mayor a 0 y menor a 1,000,000.")]
    [InlineData("Café", "1.001", null, "El precio puede tener máximo 2 decimales.")]
    [InlineData("Café", "25", "00000000-0000-0000-0000-000000000000", "La categoría no es válida.")]
    public void Cada_regla_da_su_mensaje_en_espanol(string nombre, string precio, string? categoria, string mensaje)
    {
        var dto = new GuardarProductoDto
        {
            Nombre = nombre,
            Precio = decimal.Parse(precio, System.Globalization.CultureInfo.InvariantCulture),
            IdCategoria = categoria is null ? null : Guid.Parse(categoria)
        };

        var resultados = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), resultados, validateAllProperties: true);

        Assert.Equal(mensaje, Assert.Single(resultados).ErrorMessage);
    }

    [Fact]
    public void Categoria_vacia_es_invalida()
    {
        var dto = Valido();
        dto.IdCategoria = Guid.Empty;

        Assert.Contains(nameof(GuardarProductoDto.IdCategoria), Errores(dto));
    }
}
