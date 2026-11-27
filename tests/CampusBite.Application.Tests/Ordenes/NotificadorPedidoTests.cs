using CampusBite.Application.Common;
using CampusBite.Application.Common.Correo;
using CampusBite.Application.Ordenes;
using CampusBite.Application.Ordenes.Dtos;
using CampusBite.Application.Tests.Auth;
using CampusBite.Application.Tests.Productos;
using CampusBite.Domain.Cafeterias;
using CampusBite.Domain.Common.Exceptions;
using CampusBite.Domain.Ordenes;
using Microsoft.Extensions.Logging;

namespace CampusBite.Application.Tests.Ordenes;

// Seguimiento del pedido por correo: a quién se avisa, qué lleva cada aviso y que un fallo del aviso nunca rompe la orden.
public class NotificadorPedidoTests
{
    private static readonly Guid IdOrden = Guid.Parse("00000000-0000-0000-0000-0000000000f1");
    private static readonly Guid IdAlumno = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

    private readonly FakeOrdenRepositorio _ordenes = new();
    private readonly FakeEnviadorCorreo _correo = new();
    private readonly FakeLogger<NotificadorPedido> _logger = new();
    private readonly NotificadorPedido _notificador;

    public NotificadorPedidoTests()
    {
        _notificador = new NotificadorPedido(_ordenes, _correo, _logger);
    }

    private static OrdenDto Orden(EstadoOrden estado = EstadoOrden.Confirmada, string? motivo = null) => new()
    {
        IdOrden = IdOrden,
        IdCafeteria = Guid.NewGuid(),
        Cafeteria = "Cafetería Central",
        PuntoEntrega = "Barra principal",
        Estado = estado,
        RecogidaEn = new DateTimeOffset(2026, 11, 28, 19, 0, 0, TimeSpan.Zero),
        Total = 110m,
        Metodo = MetodoPago.Efectivo,
        EstadoPago = EstadoPago.Pendiente,
        MotivoCancelacion = motivo,
        CodigoRecogida = null,                       // lo ve el alumno dueño; en el aviso sale de sp_destinatario_orden
        Lineas = [new LineaOrdenDto { IdProducto = Guid.NewGuid(), Producto = "Chilaquiles verdes", Cantidad = 2, PrecioUnitario = 55m }]
    };

    private static DestinatarioOrden Alumno(bool avisoListo = true) =>
        new(IdAlumno, "ana@uttt.edu.mx", "Ana López", avisoListo, "123456");

    // El envío corre en segundo plano: se espera a que llegue o se agote el tiempo
    private async Task<(string Correo, string Nombre, AvisoPedido Aviso)> EsperarAvisoAsync()
    {
        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (_correo.AvisosPedido.IsEmpty)
        {
            await Task.Delay(10, limite.Token);
        }
        Assert.True(_correo.AvisosPedido.TryDequeue(out var enviado));
        return enviado;
    }

    private async Task AssertSinAvisoAsync()
    {
        await Task.Delay(150);                       // margen para un envío en segundo plano que NO debe ocurrir
        Assert.True(_correo.AvisosPedido.IsEmpty);
    }

    [Theory]
    [InlineData(EventoPedido.Creada)]
    [InlineData(EventoPedido.PagoConfirmado)]
    [InlineData(EventoPedido.EnPreparacion)]
    [InlineData(EventoPedido.Entregada)]
    public async Task Cada_cambio_de_estado_avisa_al_dueno_con_el_resumen_y_sin_el_codigo_de_recogida(EventoPedido evento)
    {
        _ordenes.Destinatario = Alumno();

        await _notificador.NotificarAsync(evento, Orden(), IdAlumno);

        var (correo, nombre, aviso) = await EsperarAvisoAsync();
        Assert.Equal(("ana@uttt.edu.mx", "Ana López"), (correo, nombre));
        Assert.Equal((evento, IdOrden, "Cafetería Central", "Barra principal", 110m), (aviso.Evento, aviso.IdOrden, aviso.Cafeteria, aviso.PuntoEntrega, aviso.Total));
        var linea = Assert.Single(aviso.Lineas);
        Assert.Equal(("Chilaquiles verdes", 2, 55m), (linea.Producto, linea.Cantidad, linea.PrecioUnitario));
        Assert.Null(aviso.CodigoRecogida);
        Assert.Null(aviso.MotivoCancelacion);
    }

    [Fact]
    public async Task El_aviso_de_lista_lleva_el_codigo_de_recogida_del_alumno()
    {
        _ordenes.Destinatario = Alumno();

        await _notificador.NotificarAsync(EventoPedido.Lista, Orden(EstadoOrden.Lista), IdAlumno);

        Assert.Equal("123456", (await EsperarAvisoAsync()).Aviso.CodigoRecogida);
    }

    [Fact]
    public async Task El_aviso_de_cancelacion_lleva_el_motivo_y_ningun_otro_aviso_lo_lleva()
    {
        _ordenes.Destinatario = Alumno();

        await _notificador.NotificarAsync(EventoPedido.Cancelada, Orden(EstadoOrden.Cancelada, "Se acabó el producto"), IdAlumno);

        Assert.Equal("Se acabó el producto", (await EsperarAvisoAsync()).Aviso.MotivoCancelacion);
    }

    [Fact]
    public async Task Si_el_alumno_apago_el_aviso_de_pedido_listo_no_se_le_manda_ese_correo()
    {
        _ordenes.Destinatario = Alumno(avisoListo: false);

        await _notificador.NotificarAsync(EventoPedido.Lista, Orden(EstadoOrden.Lista), IdAlumno);

        await AssertSinAvisoAsync();
        Assert.Contains(_logger.Registros, r => r.Nivel == LogLevel.Information && r.Mensaje.Contains("apagó ese aviso"));
    }

    [Fact]
    public async Task Apagar_el_aviso_de_pedido_listo_no_afecta_a_los_demas_avisos()
    {
        _ordenes.Destinatario = Alumno(avisoListo: false);

        await _notificador.NotificarAsync(EventoPedido.EnPreparacion, Orden(EstadoOrden.EnPreparacion), IdAlumno);

        Assert.Equal(EventoPedido.EnPreparacion, (await EsperarAvisoAsync()).Aviso.Evento);
    }

    [Fact]
    public async Task Sin_destinatario_no_se_manda_nada_y_no_falla()
    {
        _ordenes.Destinatario = null;               // correo sin verificar, usuario inactivo u orden inexistente

        await _notificador.NotificarAsync(EventoPedido.Creada, Orden(), IdAlumno);

        await AssertSinAvisoAsync();
        Assert.Contains(_logger.Registros, r => r.Nivel == LogLevel.Information && r.Mensaje.Contains(IdOrden.ToString()));
    }

    [Fact]
    public async Task Si_no_se_puede_leer_el_destinatario_la_operacion_sigue_y_el_log_no_lleva_datos_personales()
    {
        _ordenes.ErrorAlLeer = new InvalidOperationException("ana@uttt.edu.mx 123456");

        await _notificador.NotificarAsync(EventoPedido.Creada, Orden(), IdAlumno);       // no lanza

        await AssertSinAvisoAsync();
        var aviso = Assert.Single(_logger.Registros, r => r.Nivel == LogLevel.Warning);
        Assert.Contains("InvalidOperationException", aviso.Mensaje);
        Assert.DoesNotContain("ana@uttt.edu.mx", aviso.Mensaje);
        Assert.DoesNotContain("123456", aviso.Mensaje);
    }

    [Fact]
    public async Task Una_cancelacion_del_cliente_al_leer_el_destinatario_se_propaga()
    {
        using var cancelada = new CancellationTokenSource();
        await cancelada.CancelAsync();
        _ordenes.ErrorAlLeer = new OperationCanceledException(cancelada.Token);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _notificador.NotificarAsync(EventoPedido.Creada, Orden(), IdAlumno, cancelada.Token));
    }

    [Fact]
    public async Task Si_el_SMTP_falla_no_se_propaga_y_el_log_solo_lleva_el_tipo_de_error()
    {
        _ordenes.Destinatario = Alumno();
        _correo.Error = new ServiceUnavailableException("ana@uttt.edu.mx rechazado");

        await _notificador.NotificarAsync(EventoPedido.Lista, Orden(EstadoOrden.Lista), IdAlumno);   // no lanza

        using var limite = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!_logger.Registros.Any(r => r.Nivel == LogLevel.Warning))
        {
            await Task.Delay(10, limite.Token);
        }
        var aviso = Assert.Single(_logger.Registros, r => r.Nivel == LogLevel.Warning);
        Assert.Contains("ServiceUnavailableException", aviso.Mensaje);
        Assert.DoesNotContain("ana@uttt.edu.mx", aviso.Mensaje);
        Assert.DoesNotContain("123456", aviso.Mensaje);
    }

    [Fact]
    public async Task La_peticion_no_espera_al_SMTP_el_envio_sigue_despues_de_responder()
    {
        _ordenes.Destinatario = Alumno();
        var correoLento = new CorreoLento();
        var notificador = new NotificadorPedido(_ordenes, correoLento, _logger);

        await notificador.NotificarAsync(EventoPedido.Entregada, Orden(EstadoOrden.Entregada), IdAlumno)
            .WaitAsync(TimeSpan.FromSeconds(2));                // vuelve aunque el SMTP no ha respondido

        await correoLento.Iniciado.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(correoLento.Terminado);
        correoLento.Liberar();
        await correoLento.Termino.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(CancellationToken.None, correoLento.TokenRecibido);   // sin el token de la petición: ya terminó
    }

    [Fact]
    public async Task Se_pregunta_por_el_destinatario_con_el_actor_que_hizo_el_cambio()
    {
        var encargado = Guid.NewGuid();
        _ordenes.Destinatario = Alumno();

        await _notificador.NotificarAsync(EventoPedido.EnPreparacion, Orden(), encargado);

        Assert.Equal((IdOrden, encargado), _ordenes.UltimaConsulta);
    }

    // IEnviadorCorreo que no termina hasta que la prueba lo libera (un SMTP lento)
    private sealed class CorreoLento : FakeEnviadorCorreo, IEnviadorCorreo
    {
        private readonly TaskCompletionSource _libre = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Iniciado { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Termino { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Terminado => Termino.Task.IsCompleted;
        public CancellationToken TokenRecibido { get; private set; }

        public void Liberar() => _libre.SetResult();

        public new async Task EnviarEstadoPedidoAsync(string correo, string nombre, AvisoPedido aviso, CancellationToken cancellationToken = default)
        {
            TokenRecibido = cancellationToken;
            Iniciado.SetResult();
            await _libre.Task;
            Termino.SetResult();
        }
    }

    private sealed class FakeOrdenRepositorio : IOrdenRepository
    {
        public DestinatarioOrden? Destinatario { get; set; }
        public Exception? ErrorAlLeer { get; set; }
        public (Guid IdOrden, Guid IdActor)? UltimaConsulta { get; private set; }

        public Task<DestinatarioOrden?> DestinatarioAsync(Guid idOrden, Guid idActor, CancellationToken cancellationToken = default)
        {
            UltimaConsulta = (idOrden, idActor);
            return ErrorAlLeer is not null ? throw ErrorAlLeer : Task.FromResult(Destinatario);
        }

        public Task<Guid> CrearAsync(Guid idAlumno, Guid idCafeteria, DateTimeOffset recogidaEn, MetodoPago metodo,
            IReadOnlyList<ItemOrdenDto> items, string? notas, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PaginaDto<OrdenDto>> MisOrdenesAsync(Guid idAlumno, int pagina, int tamanoPagina, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<OrdenDto> ObtenerAsync(Guid idOrden, Guid idActor, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CancelarAsync(Guid idOrden, Guid idAlumno, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<OrdenDto>> TableroAsync(Guid idCafeteria, Guid idActor, DateOnly? fecha, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task CambiarEstadoAsync(Guid idOrden, EstadoOrden estado, Guid idActor, string? motivo, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task EntregarAsync(Guid idOrden, string codigo, Guid idActor, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ConfirmarPagoAsync(Guid idOrden, Guid idActor, string? referencia, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> ReportarAsync(Guid idOrden, Guid idAlumno, MotivoReporte motivo, string? detalle, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
