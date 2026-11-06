-- JJ-Sprint4 27/10/2026: TEC-09. Lista TODOS los productos de una cafetería (disponibles e inactivos) para el
-- panel de administración del encargado. La vista vw_menu_cafeteria ya existe pero solo devuelve productos
-- disponibles de cafeterías activas; se necesita un SP separado para el panel admin.
--   CALL sp_listar_productos_cafeteria(<id_cafeteria>, <id_actor>, NULL);
-- Solo su encargado o admin_sistema. Devuelve [] si la cafetería no tiene productos.
CREATE OR REPLACE PROCEDURE sp_listar_productos_cafeteria(
  IN  p_id_cafeteria UUID,
  IN  p_id_actor     UUID,
  OUT p_productos    JSONB
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  CALL sp_exigir_gestor_cafeteria(p_id_cafeteria, p_id_actor, 'listar productos de esta cafetería');

  SELECT COALESCE(jsonb_agg(
           jsonb_build_object(
             'id_producto',    p.id_producto,
             'id_cafeteria',   p.id_cafeteria,
             'id_categoria',   p.id_categoria,
             'categoria',      cat.nombre,
             'nombre',         p.nombre,
             'descripcion',    p.descripcion,
             'precio',         p.precio,
             'imagen_url',     p.imagen_url,
             'disponible',     p.disponible,
             'creado_en',      p.creado_en,
             'actualizado_en', p.actualizado_en
           ) ORDER BY cat.nombre NULLS LAST, p.nombre
         ), '[]'::jsonb)
    INTO p_productos
    FROM producto p
    LEFT JOIN categoria cat ON cat.id_categoria = p.id_categoria
   WHERE p.id_cafeteria = p_id_cafeteria;
END $$;
