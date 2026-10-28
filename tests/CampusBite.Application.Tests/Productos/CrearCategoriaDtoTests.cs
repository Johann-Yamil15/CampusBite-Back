using System.ComponentModel.DataAnnotations;
using CampusBite.Application.Productos.Dtos;

namespace CampusBite.Application.Tests.Productos;

// JY-Sprint4 28/10/2026: TEC-09. Validaciones de CrearCategoriaDto (las mismas que el CHECK de categoria.nombre)
public class CrearCategoriaDtoTests
{
    private static List<ValidationResult> Errores(string nombre)
    {
        var dto = new CrearCategoriaDto { Nombre = nombre };
        var resultados = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), resultados, validateAllProperties: true);
        return resultados;
    }

    [Theory]
    [InlineData("Té")]
    [InlineData(" Té ")]
    [InlineData("Bebidas")]
    public void Nombre_de_2_a_60_caracteres_es_valido(string nombre)
    {
        Assert.Empty(Errores(nombre));
    }

    [Fact]
    public void Nombre_de_menos_de_2_caracteres_sin_espacios_es_invalido()
    {
        var error = Assert.Single(Errores(" A "));     // 3 caracteres, pero 1 sin espacios

        Assert.Equal("El nombre debe tener al menos 2 caracteres.", error.ErrorMessage);
        Assert.Equal([nameof(CrearCategoriaDto.Nombre)], error.MemberNames);
    }

    [Fact]
    public void Nombre_en_blanco_es_obligatorio()
    {
        Assert.Equal("El nombre es obligatorio.", Assert.Single(Errores("   ")).ErrorMessage);
    }

    [Fact]
    public void Nombre_de_mas_de_60_caracteres_es_invalido()
    {
        var error = Assert.Single(Errores(new string('x', 61)));

        Assert.Equal("El nombre puede tener máximo 60 caracteres.", error.ErrorMessage);
    }

    [Fact]
    public void Un_dto_nuevo_no_tiene_nombre()
    {
        Assert.Equal(string.Empty, new CrearCategoriaDto().Nombre);
    }
}
