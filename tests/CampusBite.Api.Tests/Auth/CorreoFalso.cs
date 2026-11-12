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
}
