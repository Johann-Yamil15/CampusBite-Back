using System.ComponentModel.DataAnnotations;
using CampusBite.Application.Auth.Dtos;
using CampusBite.Application.Cafeterias.Dtos;
using CampusBite.Domain.Cafeterias;

namespace CampusBite.Application.Tests.Cafeterias;

// Reglas de Validate() de RegistroCafeteriaRequestDto (autorregistro de negocio) y GuardarCafeteriaDto (CRUD de admin).
// [MinLength] deja pasar "  " (2 espacios); estas reglas revisan el texto recortado, como la BD (length(btrim(...))),
// y rechazan listas de métodos vacías, nulas o con valores que no existen en el enum (JSON acepta números).
// Se llama a Validate() directamente: Validator.TryValidateObject no lo ejecuta si ya falló un atributo.
public class ValidacionCafeteriaDtosTests
{
    private static RegistroCafeteriaRequestDto Registro() => new()
    {
        NombreEncargado = "Ana López", Correo = "ana@uttt.edu.mx", Password = "Password123", Telefono = "7711234567",
        NombreCafeteria = "Café Norte", Ubicacion = "Edificio B", EsDelCampus = true, PuntoEntrega = "Ventanilla",
        MetodosPago = [MetodoPago.Efectivo]
    };

    private static GuardarCafeteriaDto Guardar() => new()
    {
        Nombre = "Café Norte", Ubicacion = "Edificio B", EsDelCampus = false, PuntoEntrega = "Ventanilla",
        MetodosPago = [MetodoPago.Tarjeta, MetodoPago.Transferencia]
    };

    private static List<(string Campo, string Mensaje)> Errores(IValidatableObject dto) =>
        dto.Validate(new ValidationContext(dto))
            .Select(r => (Assert.Single(r.MemberNames), r.ErrorMessage!))
            .ToList();

    // ---- RegistroCafeteriaRequestDto ----

    [Fact]
    public void Registro_valido_no_tiene_errores_ni_por_atributos_ni_por_reglas()
    {
        var dto = Registro();

        Assert.Empty(Errores(dto));
        Assert.True(Validator.TryValidateObject(dto, new ValidationContext(dto), [], validateAllProperties: true));
    }

    [Theory]
    [InlineData("  ", "Café Norte", "NombreEncargado")]          // solo espacios
    [InlineData(" A ", "Café Norte", "NombreEncargado")]          // 1 carácter útil
    [InlineData("Ana", "   ", "NombreCafeteria")]
    [InlineData("Ana", " B ", "NombreCafeteria")]
    public void Registro_con_nombres_cortos_al_recortar_se_rechaza(string encargado, string cafeteria, string campo)
    {
        var dto = Registro();
        dto.NombreEncargado = encargado;
        dto.NombreCafeteria = cafeteria;

        Assert.Equal([(campo, "El nombre debe tener al menos 2 caracteres.")], Errores(dto));
    }

    [Fact]
    public void Registro_con_ubicacion_y_punto_en_blanco_reporta_los_dos()
    {
        var dto = Registro();
        dto.Ubicacion = "  ";
        dto.PuntoEntrega = "\t";

        Assert.Equal(
            [("Ubicacion", "La ubicación es obligatoria."), ("PuntoEntrega", "El punto de entrega es obligatorio.")],
            Errores(dto));
    }

    [Fact]
    public void Registro_sin_metodos_de_pago_vacio_o_nulo_se_rechaza()
    {
        var vacio = Registro();
        vacio.MetodosPago = [];
        var nulo = Registro();
        nulo.MetodosPago = null!;

        Assert.Equal([("MetodosPago", "La cafetería debe aceptar al menos un método de pago.")], Errores(vacio));
        Assert.Equal([("MetodosPago", "La cafetería debe aceptar al menos un método de pago.")], Errores(nulo));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(-1)]
    public void Registro_con_un_metodo_que_no_existe_en_el_enum_se_rechaza(int valor)
    {
        var dto = Registro();
        dto.MetodosPago = [MetodoPago.Efectivo, (MetodoPago)valor];

        Assert.Equal([("MetodosPago", "Método de pago no válido. Usa: Tarjeta, Transferencia o Efectivo.")], Errores(dto));
    }

    [Fact]
    public void Registro_con_todo_mal_reporta_cada_campo_una_vez()
    {
        var dto = Registro();
        dto.NombreEncargado = " ";
        dto.NombreCafeteria = "";
        dto.Ubicacion = "";
        dto.PuntoEntrega = "";
        dto.MetodosPago = [];

        Assert.Equal(["NombreEncargado", "NombreCafeteria", "Ubicacion", "PuntoEntrega", "MetodosPago"],
            Errores(dto).Select(e => e.Campo));
    }

    // ---- GuardarCafeteriaDto ----

    [Fact]
    public void Guardar_valido_no_tiene_errores()
    {
        var dto = Guardar();

        Assert.Empty(Errores(dto));
        Assert.True(Validator.TryValidateObject(dto, new ValidationContext(dto), [], validateAllProperties: true));
    }

    [Theory]
    [InlineData("  ")]
    [InlineData(" x ")]
    [InlineData("")]
    public void Guardar_con_nombre_corto_al_recortar_se_rechaza(string nombre)
    {
        var dto = Guardar();
        dto.Nombre = nombre;

        Assert.Equal([("Nombre", "El nombre debe tener al menos 2 caracteres.")], Errores(dto));
    }

    [Fact]
    public void Guardar_con_ubicacion_y_punto_en_blanco_reporta_los_dos()
    {
        var dto = Guardar();
        dto.Ubicacion = " ";
        dto.PuntoEntrega = "  ";

        Assert.Equal(["Ubicacion", "PuntoEntrega"], Errores(dto).Select(e => e.Campo));
    }

    [Fact]
    public void Guardar_sin_metodos_o_con_uno_inexistente_se_rechaza()
    {
        var nulo = Guardar();
        nulo.MetodosPago = null!;
        var invalido = Guardar();
        invalido.MetodosPago = [(MetodoPago)99];

        Assert.Equal([("MetodosPago", "La cafetería debe aceptar al menos un método de pago.")], Errores(nulo));
        Assert.Equal([("MetodosPago", "Método de pago no válido. Usa: Tarjeta, Transferencia o Efectivo.")], Errores(invalido));
    }

    [Fact]
    public void Los_atributos_rechazan_lo_que_falta_y_los_limites_de_longitud()
    {
        var dto = new GuardarCafeteriaDto { Nombre = new string('x', 101), Ubicacion = new string('u', 151), PuntoEntrega = "" };
        var resultados = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(dto, new ValidationContext(dto), resultados, validateAllProperties: true));

        var campos = resultados.SelectMany(r => r.MemberNames).ToHashSet();
        Assert.Superset(new HashSet<string> { "Nombre", "Ubicacion", "PuntoEntrega", "EsDelCampus" }, campos);
        Assert.Contains(resultados, r => r.ErrorMessage == "El nombre puede tener máximo 100 caracteres.");
        Assert.Contains(resultados, r => r.ErrorMessage == "La ubicación puede tener máximo 150 caracteres.");
        Assert.Contains(resultados, r => r.ErrorMessage == "Indica si la cafetería es del campus.");
    }
}
