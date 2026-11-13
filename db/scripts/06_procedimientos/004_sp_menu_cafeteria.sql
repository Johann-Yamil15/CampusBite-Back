-- JY-Sprint2 09/10/2026: menú de una cafetería activa (o de todas si p_id_cafeteria es NULL), con sus métodos
-- de pago. Reemplaza la lectura directa de vw_menu_cafeteria que tenía la API.
--   CALL sp_menu_cafeteria(NULL, NULL);
-- JJ-Sprint5 13/11/2026: agrega lo que pide el rediseño del alumno: abierta / cierra_a (horario y cierre de hoy),
-- imágenes y tiempo estimado de la cafetería, y por producto minutos_preparacion, dietas y popular (top 3 de piezas
-- vendidas en 30 días, sin canceladas). calificacion/resenas van NULL: no hay reseñas todavía.
-- precio_anterior va NULL: las promociones aún no se ligan a productos.
-- p_menu: [{"id_cafeteria", "cafeteria", "es_del_campus", "punto_entrega", "metodos_pago": [...], "logo_url",
--           "portada_url", "calificacion", "resenas", "abierta", "cierra_a", "minutos_estimados",
--           "productos": [{"id_producto", "producto", "categoria", "descripcion", "precio", "imagen_url",
--                          "calificacion", "minutos_preparacion", "precio_anterior", "popular", "dietas"}]}]
CREATE OR REPLACE PROCEDURE sp_menu_cafeteria(
  IN  p_id_cafeteria UUID,
  OUT p_menu         JSONB
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  SELECT COALESCE(jsonb_agg(jsonb_build_object(
           'id_cafeteria',      c.id_cafeteria,
           'cafeteria',         c.nombre,
           'es_del_campus',     c.es_del_campus,
           'punto_entrega',     c.punto_entrega,
           'metodos_pago',      COALESCE(to_jsonb(mp.metodos), '[]'::jsonb),
           'logo_url',          c.logo_url,
           'portada_url',       c.portada_url,
           'calificacion',      NULL,
           'resenas',           NULL,
           'abierta',           fn_cafeteria_abierta(c.id_cafeteria),
           'cierra_a',          fn_cafeteria_cierra_a(c.id_cafeteria),
           'minutos_estimados', c.minutos_estimados,
           'productos',         COALESCE(pr.productos, '[]'::jsonb))
         ORDER BY c.nombre), '[]'::jsonb)
    INTO p_menu
    FROM cafeteria c
    LEFT JOIN vw_metodos_pago_cafeteria mp ON mp.id_cafeteria = c.id_cafeteria
    LEFT JOIN LATERAL (
      SELECT jsonb_agg(jsonb_build_object(
               'id_producto',         m.id_producto,
               'producto',            m.producto,
               'categoria',           m.categoria,
               'descripcion',         m.descripcion,
               'precio',              m.precio,
               'imagen_url',          m.imagen_url,
               'calificacion',        NULL,
               'minutos_preparacion', p.minutos_preparacion,
               'precio_anterior',     NULL,
               'popular',             m.id_producto IN (
                                        SELECT d.id_producto
                                          FROM detalle_orden d JOIN orden o ON o.id_orden = d.id_orden
                                         WHERE o.id_cafeteria = c.id_cafeteria AND o.estado <> 'cancelada'
                                           AND o.creado_en >= now() - interval '30 days'
                                         GROUP BY d.id_producto ORDER BY sum(d.cantidad) DESC LIMIT 3),
               'dietas',              to_jsonb(p.dietas))
             ORDER BY m.categoria NULLS LAST, m.producto) AS productos
        FROM vw_menu_cafeteria m
        JOIN producto p ON p.id_producto = m.id_producto
       WHERE m.id_cafeteria = c.id_cafeteria
    ) pr ON true
   WHERE c.activa
     AND (p_id_cafeteria IS NULL OR c.id_cafeteria = p_id_cafeteria);
END $$;
