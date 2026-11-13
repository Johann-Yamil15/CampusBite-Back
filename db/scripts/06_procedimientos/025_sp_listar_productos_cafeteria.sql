-- JJ-Sprint4 27/10/2026: TEC-09. Lista TODOS los productos de una cafetería (disponibles e inactivos) para el
-- panel de administración del encargado. La vista vw_menu_cafeteria ya existe pero solo devuelve productos
-- disponibles de cafeterías activas; se necesita un SP separado para el panel admin.
-- JY-Sprint4 05/11/2026: paginado, con búsqueda por nombre y filtro por categoría, para cafeterías con miles de
-- productos (antes devolvía todos de golpe: 1500 productos = ~585 KB en cada visita al panel).
--   CALL sp_listar_productos_cafeteria(<id_cafeteria>, <id_actor>, 'chila' o NULL, <id_categoria> o NULL, 20, 0, NULL, NULL);
-- p_buscar: parte del nombre, sin distinguir mayúsculas (% y _ se buscan como texto). p_id_categoria: NULL = todas.
-- p_limite: 1 a 100 (20 si es NULL). p_desplazamiento: cuántos saltar (0 si es NULL).
-- p_total: cuántos cumplen el filtro. p_productos: la página, por categoría (sin categoría al final) y nombre.
-- Solo su encargado o admin_sistema. La primera versión (p_id_cafeteria, p_id_actor, OUT p_productos) se quita.
DROP PROCEDURE IF EXISTS sp_listar_productos_cafeteria(UUID, UUID, JSONB);

CREATE OR REPLACE PROCEDURE sp_listar_productos_cafeteria(
  IN  p_id_cafeteria   UUID,
  IN  p_id_actor       UUID,
  IN  p_buscar         VARCHAR,
  IN  p_id_categoria   UUID,
  IN  p_limite         INT,
  IN  p_desplazamiento INT,
  OUT p_total          INT,
  OUT p_productos      JSONB
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_patron TEXT := '%' || replace(replace(replace(btrim(COALESCE(p_buscar, '')), '\', '\\'), '%', '\%'), '_', '\_') || '%';
BEGIN
  CALL sp_exigir_gestor_cafeteria(p_id_cafeteria, p_id_actor, 'listar productos de esta cafetería');

  SELECT count(*) INTO p_total
    FROM producto p
   WHERE p.id_cafeteria = p_id_cafeteria
     AND p.nombre ILIKE v_patron
     AND (p_id_categoria IS NULL OR p.id_categoria = p_id_categoria);

  SELECT COALESCE(jsonb_agg(to_jsonb(v) - 'orden' ORDER BY v.orden), '[]'::jsonb)
    INTO p_productos
    FROM (SELECT p.id_producto, p.id_cafeteria, p.id_categoria, cat.nombre AS categoria, p.nombre, p.descripcion,
                 p.precio, p.imagen_url, p.disponible, p.creado_en, p.actualizado_en,
                 row_number() OVER (ORDER BY cat.nombre NULLS LAST, lower(btrim(p.nombre)), p.id_producto) AS orden
            FROM producto p
            LEFT JOIN categoria cat ON cat.id_categoria = p.id_categoria
           WHERE p.id_cafeteria = p_id_cafeteria
             AND p.nombre ILIKE v_patron
             AND (p_id_categoria IS NULL OR p.id_categoria = p_id_categoria)
           ORDER BY cat.nombre NULLS LAST, lower(btrim(p.nombre)), p.id_producto
           LIMIT LEAST(GREATEST(COALESCE(p_limite, 20), 1), 100)
          OFFSET GREATEST(COALESCE(p_desplazamiento, 0), 0)) v;
END $$;
