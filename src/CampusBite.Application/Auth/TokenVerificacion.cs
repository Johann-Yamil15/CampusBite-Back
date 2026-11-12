using System.Buffers.Text;
using System.Security.Cryptography;

namespace CampusBite.Application.Auth;

// JY-Sprint4 31/10/2026: token del enlace de verificación de correo. Va en claro solo en el correo; en la BD se guarda
// su SHA-256, así que quien lea la base no puede usar los enlaces pendientes.
public static class TokenVerificacion
{
    public static readonly TimeSpan Vigencia = TimeSpan.FromHours(24);

    // 32 bytes aleatorios (256 bits) en Base64Url: se puede poner en una URL sin escapar
    public static (string Token, string Hash) Generar()
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        return (token, Hash(token));
    }

    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
}
