using CampusBite.Application.Common;

namespace CampusBite.Application.Tests.Common;

// JY-Sprint4 05/11/2026: metadatos de la paginación (los usa el frontend para los botones y el scroll infinito)
public class PaginaDtoTests
{
    private static PaginaDto<int> Pagina(int total, int pagina, int tamano) =>
        new() { Items = [], Total = total, Pagina = pagina, TamanoPagina = tamano };

    [Theory]
    [InlineData(1500, 1, 20, 75, false, true)]
    [InlineData(1500, 75, 20, 75, true, false)]
    [InlineData(1501, 75, 20, 76, true, true)]                  // sobra uno: hay página 76
    [InlineData(20, 1, 20, 1, false, false)]
    [InlineData(0, 1, 20, 0, false, false)]                     // sin resultados
    [InlineData(10, 9, 20, 1, true, false)]                     // página fuera de rango: sin siguiente
    public void Calcula_paginas_y_si_hay_anterior_o_siguiente(
        int total, int pagina, int tamano, int totalPaginas, bool anterior, bool siguiente)
    {
        var p = Pagina(total, pagina, tamano);

        Assert.Equal((totalPaginas, anterior, siguiente), (p.TotalPaginas, p.TieneAnterior, p.TieneSiguiente));
    }

    [Fact]
    public void Tamano_cero_no_divide_entre_cero()
    {
        Assert.Equal(0, Pagina(10, 1, 0).TotalPaginas);
    }

    [Fact]
    public void La_consulta_arma_la_pagina_con_sus_datos()
    {
        var consulta = new ConsultaPaginadaDto { Pagina = 3, TamanoPagina = 50, Buscar = "  café " };

        var pagina = consulta.CrearPagina(1534, [1, 2]);

        Assert.Equal((1534, 3, 50, 31), (pagina.Total, pagina.Pagina, pagina.TamanoPagina, pagina.TotalPaginas));
        Assert.Equal([1, 2], pagina.Items);
        Assert.Equal(("café", 100), (consulta.BuscarNormalizado(), consulta.Desplazamiento()));
        Assert.Null(new ConsultaPaginadaDto { Buscar = "   " }.BuscarNormalizado());
    }
}
