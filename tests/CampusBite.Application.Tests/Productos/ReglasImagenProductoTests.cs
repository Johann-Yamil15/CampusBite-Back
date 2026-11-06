using CampusBite.Application.Productos;
using CampusBite.Domain.Common.Exceptions;

namespace CampusBite.Application.Tests.Productos;

// JY-Sprint4 27/10/2026: TEC-10. Casos borde de ReglasImagenProducto que no se alcanzan desde el servicio
// (agregados tras las pruebas de mutación con Stryker)
public class ReglasImagenProductoTests
{
    private const string MensajeContenido = "El archivo no es una imagen válida o no coincide con su tipo (JPEG, PNG o WebP).";

    [Fact]
    public async Task Archivo_que_declara_tamano_pero_llega_vacio_se_rechaza()
    {
        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => ReglasImagenProducto.LeerAsync(new MemoryStream(), CancellationToken.None));

        Assert.Equal("El archivo está vacío.", error.Message);
    }

    [Fact]
    public async Task Deja_de_leer_en_cuanto_pasa_de_5_MB_sin_esperar_al_final()
    {
        var infinito = new StreamInfinito();

        var error = await Assert.ThrowsAsync<BusinessRuleException>(
            () => ReglasImagenProducto.LeerAsync(infinito, CancellationToken.None));

        Assert.Equal("La imagen pesa más de 5 MB. Redúcela e intenta de nuevo.", error.Message);
        Assert.InRange(infinito.Leidos, ReglasImagenProducto.TamanoMaximoBytes, ReglasImagenProducto.TamanoMaximoBytes + 81_920);
    }

    [Fact]
    public async Task Lo_leido_queda_al_inicio_para_poder_subirlo()
    {
        await using var copia = await ReglasImagenProducto.LeerAsync(new MemoryStream([1, 2, 3]), CancellationToken.None);

        Assert.Equal(0, copia.Position);
        Assert.Equal([1, 2, 3], copia.ToArray());
    }

    [Theory]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46 })]                                                  // "RIFF" sin más
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x41, 0x56, 0x49, 0x20 })]             // RIFF de un video AVI
    [InlineData(new byte[] { 0x00, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50 })]             // "WEBP" sin "RIFF"
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 })]             // un PNG de 12 bytes
    public void WebP_falso_o_incompleto_se_rechaza(byte[] contenido)
    {
        var error = Assert.Throws<BusinessRuleException>(() => ReglasImagenProducto.ValidarContenido(contenido, "image/webp"));

        Assert.Equal(MensajeContenido, error.Message);
    }

    [Fact]
    public void WebP_valido_se_acepta()
    {
        ReglasImagenProducto.ValidarContenido("RIFF\0\0\0\0WEBPVP8 "u8, "image/webp");
    }

    [Fact]
    public void Un_tipo_que_no_es_de_imagen_nunca_coincide()
    {
        Assert.Throws<BusinessRuleException>(() => ReglasImagenProducto.ValidarContenido([0xFF, 0xD8, 0xFF], "image/gif"));
    }

    [Theory]
    [InlineData(" image/png ")]
    [InlineData("image/png;")]
    public void Tipo_con_espacios_o_parametros_vacios_se_normaliza(string tipo)
    {
        Assert.Equal("image/png", ReglasImagenProducto.ValidarTipo(tipo));
    }

    // Entrega bytes "sin fin" (se acota a 64 MB para no agotar la memoria si alguien quita el corte) y cuenta cuántos dio
    private sealed class StreamInfinito : Stream
    {
        private const long Tope = 64L * 1024 * 1024;
        public long Leidos { get; private set; }

        public override int Read(byte[] buffer, int offset, int count) => Entregar(count);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Entregar(buffer.Length));

        private int Entregar(int pedidos)
        {
            var dados = (int)Math.Min(pedidos, Tope - Leidos);
            Leidos += dados;
            return dados;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => Leidos; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
