using CampusBite.Application.Common.Dtos;

namespace CampusBite.Application.Tests.Common;

// JY-Sprint3 20/10/2026: cálculo del total de páginas
public class PaginaDtoTests
{
    [Theory]
    [InlineData(0, 20, 0)]
    [InlineData(1, 20, 1)]
    [InlineData(20, 20, 1)]
    [InlineData(21, 20, 2)]
    [InlineData(45, 20, 3)]
    [InlineData(5, 0, 0)]      // tamaño 0 no divide entre cero
    public void TotalPaginas_redondea_hacia_arriba(int total, int tamano, int esperado)
    {
        Assert.Equal(esperado, new PaginaDto<int> { Total = total, Tamano = tamano }.TotalPaginas);
    }
}
