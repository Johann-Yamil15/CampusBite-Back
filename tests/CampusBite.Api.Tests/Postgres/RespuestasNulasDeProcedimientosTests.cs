using CampusBite.Domain.Common.Exceptions;
using CampusBite.Infrastructure.Alumnos;
using CampusBite.Infrastructure.Cafeterias;
using CampusBite.Infrastructure.Menu;
using CampusBite.Infrastructure.Negocio;
using CampusBite.Infrastructure.Ordenes;
using CampusBite.Infrastructure.Productos;

namespace CampusBite.Api.Tests.Postgres;

// Los repositorios convierten un OUT JSONB en NULL en "sin resultados" (lista vacía, null o 404) en vez de fallar.
// Los procedimientos reales nunca devuelven NULL (siempre COALESCE a '[]'), así que esas ramas defensivas solo se
// recorren si el procedimiento lo hace. Aquí, y SOLO en una base temporal propia que se borra al terminar (nunca
// Neon, nunca la base compartida de las demás pruebas), se redefinen los procedimientos para que devuelvan NULL.
public class RespuestasNulasDeProcedimientosTests(PostgresDePrueba bd) : IClassFixture<PostgresDePrueba>, IAsyncLifetime
{
    private static readonly Guid Otro = Guid.Parse("00000000-0000-0000-0000-00000000f0f0");

    // Misma firma que los procedimientos reales (CREATE OR REPLACE los sustituye), pero sin resultado
    private const string ProcedimientosSinResultado = """
        CREATE OR REPLACE PROCEDURE sp_listar_cafeterias(IN p_id_actor UUID, IN p_incluir_inactivas BOOLEAN, IN p_buscar VARCHAR,
            IN p_limite INT, IN p_desplazamiento INT, OUT p_total INT, OUT p_cafeterias JSONB)
          LANGUAGE plpgsql AS $$ BEGIN p_total := 0; p_cafeterias := NULL; END $$;
        CREATE OR REPLACE PROCEDURE sp_menu_cafeteria(IN p_id_cafeteria UUID, OUT p_menu JSONB)
          LANGUAGE plpgsql AS $$ BEGIN p_menu := NULL; END $$;
        CREATE OR REPLACE PROCEDURE sp_listar_promociones(IN p_id_cafeteria UUID, IN p_id_actor UUID, OUT p_lista JSONB)
          LANGUAGE plpgsql AS $$ BEGIN p_lista := NULL; END $$;
        CREATE OR REPLACE PROCEDURE sp_promociones_activas(OUT p_lista JSONB)
          LANGUAGE plpgsql AS $$ BEGIN p_lista := NULL; END $$;
        CREATE OR REPLACE PROCEDURE sp_cola_cafeteria(IN p_id_cafeteria UUID, IN p_id_actor UUID, IN p_fecha DATE, OUT p_cola JSONB)
          LANGUAGE plpgsql AS $$ BEGIN p_cola := NULL; END $$;
        CREATE OR REPLACE PROCEDURE sp_listar_productos_cafeteria(IN p_id_cafeteria UUID, IN p_id_actor UUID, IN p_buscar VARCHAR,
            IN p_id_categoria UUID, IN p_limite INT, IN p_desplazamiento INT, OUT p_total INT, OUT p_productos JSONB)
          LANGUAGE plpgsql AS $$ BEGIN p_total := 0; p_productos := NULL; END $$;
        CREATE OR REPLACE PROCEDURE sp_listar_categorias(IN p_id_cafeteria UUID, OUT p_categorias JSONB)
          LANGUAGE plpgsql AS $$ BEGIN p_categorias := 'null'::jsonb; END $$;
        CREATE OR REPLACE PROCEDURE sp_obtener_orden(IN p_id_orden UUID, IN p_id_actor UUID, OUT p_orden JSONB)
          LANGUAGE plpgsql AS $$ BEGIN p_orden := NULL; END $$;
        CREATE OR REPLACE PROCEDURE sp_favoritos_cafeterias(IN p_id_usuario UUID, OUT p_lista JSONB)
          LANGUAGE plpgsql AS $$ BEGIN p_lista := NULL; END $$;
        CREATE OR REPLACE PROCEDURE sp_favoritos_productos(IN p_id_usuario UUID, OUT p_lista JSONB)
          LANGUAGE plpgsql AS $$ BEGIN p_lista := NULL; END $$;
        """;

    public async Task InitializeAsync()
    {
        if (PostgresDePrueba.Disponible)
        {
            await bd.EjecutarAsync(ProcedimientosSinResultado);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [FactPostgres]
    public async Task Cafeterias_sin_resultado_son_cero_y_lista_vacia()
    {
        var (total, items) = await new CafeteriaRepository(bd.Contexto()).ListarAsync(PostgresDePrueba.Admin, false, null, 20, 0);

        Assert.Equal(0, total);
        Assert.Empty(items);
    }

    [FactPostgres]
    public async Task Menu_sin_resultado_es_lista_vacia_y_una_cafeteria_sin_menu_es_null()
    {
        var repo = new MenuRepository(bd.Contexto());

        Assert.Empty(await repo.ListarAsync());
        Assert.Null(await repo.ObtenerAsync(PostgresDePrueba.Cafeteria));
    }

    [FactPostgres]
    public async Task Promociones_sin_resultado_son_listas_vacias()
    {
        var repo = new NegocioRepository(bd.Contexto());

        Assert.Empty(await repo.ListarPromocionesAsync(PostgresDePrueba.Cafeteria, PostgresDePrueba.Encargado));
        Assert.Empty(await repo.PromocionesActivasAsync());
    }

    [FactPostgres]
    public async Task Tablero_sin_resultado_es_lista_vacia_y_una_orden_sin_datos_es_404()
    {
        var repo = new OrdenRepository(bd.Contexto());

        Assert.Empty(await repo.TableroAsync(PostgresDePrueba.Cafeteria, PostgresDePrueba.Encargado, null));
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => repo.ObtenerAsync(Otro, PostgresDePrueba.Alumno));
        Assert.Equal("La orden no existe.", ex.Message);
    }

    [FactPostgres]
    public async Task Productos_y_categorias_sin_resultado_son_listas_vacias()
    {
        var repo = new ProductoRepository(bd.Contexto());

        var (total, items) = await repo.ListarAsync(PostgresDePrueba.Cafeteria, PostgresDePrueba.Encargado, null, null, 20, 0);
        Assert.Equal(0, total);
        Assert.Empty(items);
        Assert.Empty(await repo.ListarCategoriasAsync(PostgresDePrueba.Cafeteria));
    }

    [FactPostgres]
    public async Task Favoritos_sin_resultado_son_listas_vacias()
    {
        var repo = new AlumnoRepository(bd.Contexto());

        Assert.Empty(await repo.FavoritosCafeteriasAsync(PostgresDePrueba.Alumno));
        Assert.Empty(await repo.FavoritosPlatillosAsync(PostgresDePrueba.Alumno));
    }
}
