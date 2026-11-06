-- JJ-Sprint4 27/10/2026: TEC-09. Devuelve las categorías de una cafetería. Cualquier usuario autenticado puede
-- usarlo (para poblar el selector de categoría al crear una orden o al crear un producto).
--   CALL sp_listar_categorias(<id_cafeteria>, NULL);
-- Devuelve [] si la cafetería no tiene categorías.
CREATE OR REPLACE PROCEDURE sp_listar_categorias(
  IN  p_id_cafeteria UUID,
  OUT p_categorias   JSONB
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  SELECT COALESCE(jsonb_agg(
           jsonb_build_object('id_categoria', c.id_categoria, 'nombre', c.nombre)
           ORDER BY c.nombre
         ), '[]'::jsonb)
    INTO p_categorias
    FROM categoria c
   WHERE c.id_cafeteria = p_id_cafeteria;
END $$;
