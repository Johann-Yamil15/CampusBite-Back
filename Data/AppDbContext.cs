using Microsoft.EntityFrameworkCore;

namespace CampusBite_Back.Data;

// JJ-Sprint1 04/10/2026: conexión a PostgreSQL. El esquema lo controlan los scripts de db/ (no usar migraciones de EF);
// el backend solo ejecuta los procedimientos sp_* y lee las vistas vw_* con Database.SqlQuery
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
}
