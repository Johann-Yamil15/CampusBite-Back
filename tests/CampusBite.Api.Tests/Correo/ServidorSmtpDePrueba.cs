using System.Net;
using System.Net.Sockets;
using System.Text;

namespace CampusBite.Api.Tests.Correo;

// Servidor SMTP mínimo en 127.0.0.1 para probar MailKit de punta a punta sin proveedor real ni
// credenciales reales. Habla lo justo de SMTP (EHLO, AUTH PLAIN/LOGIN, MAIL, RCPT, DATA, QUIT) y puede comportarse mal
// a propósito: rechazar la autenticación, rechazar el destinatario o no responder nunca (para el tiempo agotado).
// Como el host es local, SmtpEnviadorCorreo usa StartTlsWhenAvailable y este servidor no ofrece STARTTLS: va en claro.
public sealed class ServidorSmtpDePrueba : IAsyncDisposable
{
    public enum Modo
    {
        Acepta,
        RechazaAutenticacion,
        RechazaDestinatario,
        NoResponde
    }

    private readonly TcpListener _escucha;
    private readonly CancellationTokenSource _fin = new();
    private readonly Task _bucle;
    private readonly Modo _modo;

    public int Puerto { get; }
    public int Conexiones;
    public List<string> Mensajes { get; } = [];
    public List<string> Destinatarios { get; } = [];

    public ServidorSmtpDePrueba(Modo modo = Modo.Acepta)
    {
        _modo = modo;
        _escucha = new TcpListener(IPAddress.Loopback, 0);
        _escucha.Start();
        Puerto = ((IPEndPoint)_escucha.LocalEndpoint).Port;
        _bucle = Task.Run(AtenderAsync);
    }

    private async Task AtenderAsync()
    {
        while (!_fin.IsCancellationRequested)
        {
            TcpClient cliente;
            try
            {
                cliente = await _escucha.AcceptTcpClientAsync(_fin.Token);
            }
            catch
            {
                return;
            }
            Interlocked.Increment(ref Conexiones);
            _ = Task.Run(() => ConversarAsync(cliente));
        }
    }

    private async Task ConversarAsync(TcpClient cliente)
    {
        using var _ = cliente;
        try
        {
            var flujo = cliente.GetStream();
            var lector = new StreamReader(flujo, Encoding.UTF8);      // MailKit manda 8BITMIME en UTF-8
            var escritor = new StreamWriter(flujo, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
            if (_modo == Modo.NoResponde)
            {
                await Task.Delay(Timeout.Infinite, _fin.Token);
                return;
            }

            await escritor.WriteLineAsync("220 prueba.local ESMTP listo");
            while (await lector.ReadLineAsync(_fin.Token) is { } linea)
            {
                var comando = linea.ToUpperInvariant();
                if (comando.StartsWith("EHLO") || comando.StartsWith("HELO"))
                {
                    await escritor.WriteLineAsync("250-prueba.local");
                    await escritor.WriteLineAsync("250-AUTH PLAIN LOGIN");
                    await escritor.WriteLineAsync("250 8BITMIME");
                }
                else if (comando.StartsWith("AUTH"))
                {
                    if (comando.StartsWith("AUTH LOGIN"))
                    {
                        await escritor.WriteLineAsync("334 VXNlcm5hbWU6");
                        await lector.ReadLineAsync(_fin.Token);
                        await escritor.WriteLineAsync("334 UGFzc3dvcmQ6");
                        await lector.ReadLineAsync(_fin.Token);
                    }
                    await escritor.WriteLineAsync(_modo == Modo.RechazaAutenticacion
                        ? "535 5.7.8 Authentication credentials invalid"
                        : "235 2.7.0 Authentication successful");
                }
                else if (comando.StartsWith("MAIL FROM"))
                {
                    await escritor.WriteLineAsync("250 2.1.0 OK");
                }
                else if (comando.StartsWith("RCPT TO"))
                {
                    lock (Destinatarios) Destinatarios.Add(linea[8..].Trim());
                    await escritor.WriteLineAsync(_modo == Modo.RechazaDestinatario
                        ? "550 5.1.1 Mailbox unavailable"
                        : "250 2.1.5 OK");
                }
                else if (comando == "DATA")
                {
                    await escritor.WriteLineAsync("354 Termina con <CRLF>.<CRLF>");
                    var cuerpo = new StringBuilder();
                    while (await lector.ReadLineAsync(_fin.Token) is { } l && l != ".")
                    {
                        cuerpo.AppendLine(l.StartsWith("..") ? l[1..] : l);
                    }
                    lock (Mensajes) Mensajes.Add(cuerpo.ToString());
                    await escritor.WriteLineAsync("250 2.0.0 Aceptado");
                }
                else if (comando == "QUIT")
                {
                    await escritor.WriteLineAsync("221 2.0.0 Adiós");
                    return;
                }
                else if (comando == "RSET" || comando == "NOOP")
                {
                    await escritor.WriteLineAsync("250 OK");
                }
                else
                {
                    await escritor.WriteLineAsync("502 5.5.2 No implementado");
                }
            }
        }
        catch
        {
            // conexión cerrada por el cliente o fin de la prueba
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _fin.CancelAsync();
        _escucha.Stop();
        try { await _bucle; } catch { }
        _fin.Dispose();
    }
}
