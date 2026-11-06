namespace CampusBite.Infrastructure.Blob;

// JY-Sprint4 27/10/2026: TEC-10. Configuración de Azure Blob Storage enlazada desde la sección "AzureBlob"
// (variables AzureBlob__*). Los reintentos cubren cortes momentáneos de red entre la API y Azure: cada intento
// tiene su propio tiempo límite y la espera entre intentos crece (1 s, 2 s, 4 s...) hasta EsperaMaximaSegundos.
public sealed class AzureBlobSettings
{
    public const string SectionName = "AzureBlob";

    public string ConnectionString { get; set; } = string.Empty;
    public string ContainerProductos { get; set; } = "productos";

    public int MaxReintentos { get; set; } = 4;
    public double EsperaInicialSegundos { get; set; } = 1;
    public double EsperaMaximaSegundos { get; set; } = 10;
    public double TiempoPorIntentoSegundos { get; set; } = 30;
}
