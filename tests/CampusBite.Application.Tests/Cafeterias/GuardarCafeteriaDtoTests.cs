using System.ComponentModel.DataAnnotations;
using CampusBite.Application.Cafeterias.Dtos;
using CampusBite.Domain.Cafeterias;

namespace CampusBite.Application.Tests.Cafeterias;

// JY-Sprint3 20/10/2026: TEC-08. Reglas de validación del cuerpo de POST/PUT (antes solo se probaban por la API;
// las pruebas de mutación mostraron que aquí no las cubría nada). Se valida igual que ASP.NET: atributos + Validate().
public class GuardarCafeteriaDtoTests
{
    private static GuardarCafeteriaDto Valido() => new()
    {
        Nombre = "Cafetería Norte", Ubicacion = "Edificio K", EsDelCampus = false, PuntoEntrega = "Ventanilla 2",
        MetodosPago = [MetodoPago.Efectivo]
    };

    private static List<ValidationResult> Validar(GuardarCafeteriaDto dto)
    {
        var errores = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), errores, validateAllProperties: true);
        return errores;
    }

    private static void AssertError(GuardarCafeteriaDto dto, string campo, string mensaje)
    {
        var errores = Validar(dto);
        Assert.Contains(errores, e => e.MemberNames.Contains(campo) && e.ErrorMessage == mensaje);
    }

    [Fact]
    public void Datos_validos_no_tienen_errores()
    {
        Assert.Empty(Validar(Valido()));
    }

    [Theory]
    [InlineData("A")]           // 1 carácter
    [InlineData(" A ")]         // 1 carácter después de quitar espacios (MinLength no lo detecta)
    public void Nombre_de_menos_de_2_caracteres_reales_es_invalido(string nombre)
    {
        var dto = Valido();
        dto.Nombre = nombre;

        AssertError(dto, nameof(dto.Nombre), "El nombre debe tener al menos 2 caracteres.");
    }

    [Fact]
    public void Nombre_de_exactamente_2_caracteres_es_valido()
    {
        var dto = Valido();
        dto.Nombre = " AB ";

        Assert.Empty(Validar(dto));
    }

    [Fact]
    public void Nombre_obligatorio_y_maximo_100()
    {
        var vacio = Valido();
        vacio.Nombre = "";
        AssertError(vacio, nameof(vacio.Nombre), "El nombre es obligatorio.");

        var espacios = Valido();
        espacios.Nombre = "   ";
        AssertError(espacios, nameof(espacios.Nombre), "El nombre es obligatorio.");

        var largo = Valido();
        largo.Nombre = new string('x', 101);
        AssertError(largo, nameof(largo.Nombre), "El nombre puede tener máximo 100 caracteres.");

        var limite = Valido();
        limite.Nombre = new string('x', 100);
        Assert.Empty(Validar(limite));
    }

    [Fact]
    public void Ubicacion_obligatoria_sin_contar_espacios_y_maximo_150()
    {
        var espacios = Valido();
        espacios.Ubicacion = "   ";
        AssertError(espacios, nameof(espacios.Ubicacion), "La ubicación es obligatoria.");

        var largo = Valido();
        largo.Ubicacion = new string('x', 151);
        AssertError(largo, nameof(largo.Ubicacion), "La ubicación puede tener máximo 150 caracteres.");
    }

    [Fact]
    public void PuntoEntrega_obligatorio_sin_contar_espacios_y_maximo_100()
    {
        var espacios = Valido();
        espacios.PuntoEntrega = "   ";
        AssertError(espacios, nameof(espacios.PuntoEntrega), "El punto de entrega es obligatorio.");

        var largo = Valido();
        largo.PuntoEntrega = new string('x', 101);
        AssertError(largo, nameof(largo.PuntoEntrega), "El punto de entrega puede tener máximo 100 caracteres.");
    }

    [Fact]
    public void EsDelCampus_no_enviado_es_error_y_false_es_valido()
    {
        var sinValor = Valido();
        sinValor.EsDelCampus = null;
        AssertError(sinValor, nameof(sinValor.EsDelCampus), "Indica si la cafetería es del campus.");

        Assert.Empty(Validar(Valido()));   // Valido() usa false a propósito
    }

    [Fact]
    public void Sin_metodos_de_pago_es_invalido()
    {
        var dto = Valido();
        dto.MetodosPago = [];

        AssertError(dto, nameof(dto.MetodosPago), "La cafetería debe aceptar al menos un método de pago.");
    }

    [Fact]
    public void Metodo_de_pago_que_no_existe_es_invalido_aunque_haya_otro_valido()
    {
        var dto = Valido();
        dto.MetodosPago = [MetodoPago.Efectivo, (MetodoPago)99];

        AssertError(dto, nameof(dto.MetodosPago), "Método de pago no válido. Usa: Tarjeta, Transferencia o Efectivo.");
    }

    [Fact]
    public void Varios_metodos_validos_no_tienen_errores()
    {
        var dto = Valido();
        dto.MetodosPago = [MetodoPago.Tarjeta, MetodoPago.Transferencia, MetodoPago.Efectivo];

        Assert.Empty(Validar(dto));
    }
}
