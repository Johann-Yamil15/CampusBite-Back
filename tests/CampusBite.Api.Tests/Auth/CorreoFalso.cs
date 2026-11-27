using CampusBite.Application.Common.Correo;

namespace CampusBite.Api.Tests.Auth;

// JY-Sprint4 31/10/2026: guarda los correos de verificación en lugar de mandarlos; con Error simula que el SMTP falla
internal sealed class CorreoFalso : IEnviadorCorreo
{
    public List<(string Correo, string Nombre, string Token)> Enviados { get; } = [];
    public Exception? Error { get; set; }

    public Task EnviarVerificacionAsync(string correo, string nombre, string token, CancellationToken cancellationToken = default)
    {
        if (Error is not null)
        {
            throw Error;
        }

        Enviados.Add((correo, nombre, token));
        return Task.CompletedTask;
    }

    // JY-Sprint4 12/11/2026: AUD-001
    public List<(string Correo, string Nombre, string Token)> Recuperaciones { get; } = [];

    public Task EnviarRecuperacionAsync(string correo, string nombre, string token, CancellationToken cancellationToken = default)
    {
        if (Error is not null)
        {
            throw Error;
        }

        Recuperaciones.Add((correo, nombre, token));
        return Task.CompletedTask;
    }

    // JY-Sprint5 27/11/2026: avisos del seguimiento del pedido. Cola concurrente: el envío real corre en segundo plano.
    public System.Collections.Concurrent.ConcurrentQueue<(string Correo, string Nombre, AvisoPedido Aviso)> AvisosPedido { get; } = new();

    public Task EnviarEstadoPedidoAsync(string correo, string nombre, AvisoPedido aviso, CancellationToken cancellationToken = default)
    {
        if (Error is not null)
        {
            throw Error;
        }

        AvisosPedido.Enqueue((correo, nombre, aviso));
        return Task.CompletedTask;
    }
}
