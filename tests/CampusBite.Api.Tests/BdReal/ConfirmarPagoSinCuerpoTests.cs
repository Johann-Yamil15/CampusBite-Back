namespace CampusBite.Api.Tests.BdReal;

// La referencia del pago es opcional: confirmar sin cuerpo es válido (el parámetro del controlador es nullable)
public class ConfirmarPagoSinCuerpoTests(BdDePrueba bd) : PruebaConBd(bd)
{
    [SkippableFact]
    public async Task Confirmar_el_pago_sin_cuerpo_ni_referencia_confirma_la_orden()
    {
        var id = await CrearOrdenAsync("Transferencia");

        var confirmada = await Json(await ComoEncargado().PostAsync($"/api/ordenes/{id}/confirmar-pago", content: null));

        Assert.Equal("Confirmada", (string?)confirmada["estado"]);
        Assert.Equal("Pagado", (string?)confirmada["estadoPago"]);
    }
}
