-- JY-Sprint2 09/10/2026: menú de una cafetería activa (o de todas si p_id_cafeteria es NULL), con sus métodos
-- de pago. Reemplaza la lectura directa de vw_menu_cafeteria que tenía la API.
--   CALL sp_menu_cafeteria(NULL, NULL);
-- p_menu: [{"id_cafeteria", "cafeteria", "es_del_campus", "punto_entrega", "metodos_pago": [...],
--           "productos": [{"id_producto", "producto", "categoria", "descripcion", "precio", "imagen_url"}]}]
CREATE OR REPLACE PROCEDURE sp_menu_cafeteria(
  IN  p_id_cafeteria UUID,
  OUT p_menu         JSONB
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  SELECT COALESCE(jsonb_agg(jsonb_build_object(
           'id_cafeteria',  c.id_cafeteria,
           'cafeteria',     c.nombre,
           'es_del_campus', c.es_del_campus,
           'punto_entrega', c.punto_entrega,
           'metodos_pago',  COALESCE(to_jsonb(mp.metodos), '[]'::jsonb),
           'productos',     COALESCE(pr.productos, '[]'::jsonb))
         ORDER BY c.nombre), '[]'::jsonb)
    INTO p_menu
    FROM cafeteria c
    LEFT JOIN vw_metodos_pago_cafeteria mp ON mp.id_cafeteria = c.id_cafeteria
    LEFT JOIN LATERAL (
      SELECT jsonb_agg(jsonb_build_object(
               'id_producto', m.id_producto,
               'producto',    m.producto,
               'categoria',   m.categoria,
               'descripcion', m.descripcion,
               'precio',      m.precio,
               'imagen_url',  m.imagen_url)
             ORDER BY m.categoria NULLS LAST, m.producto) AS productos
        FROM vw_menu_cafeteria m
       WHERE m.id_cafeteria = c.id_cafeteria
    ) pr ON true
   WHERE c.activa
     AND (p_id_cafeteria IS NULL OR c.id_cafeteria = p_id_cafeteria);
END $$;
