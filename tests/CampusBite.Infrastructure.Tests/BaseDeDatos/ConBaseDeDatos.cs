using CampusBite.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CampusBite.Infrastructure.Tests.BaseDeDatos;

// JY-Sprint3 20/10/2026: pruebas de integración contra una PostgreSQL real con la versión actual del script
// consolidado (con datos de prueba). Solo corren si existe la variable CAMPUSBITE_TEST_DB; si no, se omiten
// (así el CI sin base sigue en verde). Se conectan como app_backend: también prueban los GRANT.
//   CAMPUSBITE_TEST_DB="Host=localhost;Port=5432;Database=pruebas;Username=app_backend;Password=app_backend_dev"
public sealed class FactConBaseDeDatos : FactAttribute
{
    public FactConBaseDeDatos()
    {
        if (string.IsNullOrWhiteSpace(ConBaseDeDatos.Cadena))
            Skip = "Define CAMPUSBITE_TEST_DB para correr las pruebas contra PostgreSQL.";
    }
}

public static class ConBaseDeDatos
{
    public static string? Cadena => Environment.GetEnvironmentVariable("CAMPUSBITE_TEST_DB");

    // Usuarios y cafetería de 90_datos_dev/002_datos_prueba.sql
    public static readonly Guid Alumno = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    public static readonly Guid Encargado = Guid.Parse("00000000-0000-0000-0000-0000000000a2");
    public static readonly Guid AdminSistema = Guid.Parse("00000000-0000-0000-0000-0000000000a3");
    public static readonly Guid CafeteriaDePrueba = Guid.Parse("00000000-0000-0000-0000-0000000000c1");

    public static AppDbContext Contexto() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Cadena).UseSnakeCaseNamingConvention().Options);

    // Nombre único por prueba: los CALL hacen COMMIT, así que las pruebas no chocan entre corridas
    public static string NombreUnico(string prefijo) => $"{prefijo} {Guid.NewGuid().ToString("N")[..12]}";
}
