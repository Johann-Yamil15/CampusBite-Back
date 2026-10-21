-- JY-Sprint2 09/10/2026: menú de una cafetería activa, con sus métodos de pago. Reemplaza la lectura directa
-- de vw_menu_cafeteria que tenía la API.
--   CALL sp_menu_cafeteria(<id_cafeteria>, NULL);
-- JY-Sprint3 20/10/2026: la cafetería es obligatoria. Con NULL devolvía TODAS con TODOS sus productos: las pruebas
-- de estrés midieron 3 s y 29 MB con 10 mil cafeterías. Para ver todas está sp_listar_cafeterias (paginado).
-- Devuelve un arreglo de 0 o 1 elementos (vacío si no existe o está inactiva).
-- p_menu: [{"id_cafeteria", "cafeteria", "es_del_campus", "punto_entrega", "metodos_pago": [...],
--           "productos": [{"id_producto", "producto", "categoria", "descripcion", "precio", "imagen_url"}]}]
CREATE OR REPLACE PROCEDURE sp_menu_cafeteria(
  IN  p_id_cafeteria UUID,
  OUT p_menu         JSONB
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  IF p_id_cafeteria IS NULL THEN
    RAISE EXCEPTION 'Indica la cafetería del menú' USING ERRCODE = 'check_violation';
  END IF;

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
     AND c.id_cafeteria = p_id_cafeteria;
END $$;
