-- JJ-Sprint5 13/11/2026: funciones internas (la API no las ejecuta; las usan los procedimientos SECURITY DEFINER).
--  * fn_cafeteria_abierta: regla §5.6 de la especificación (horario de hoy y "cerrado hoy"), hora de México.
--  * fn_cafeteria_cierra_a: "HH:MI" del último cierre de hoy; NULL si hoy no abre o se cerró por hoy.
--  * fn_orden_json: la orden completa como la consume el front (OrdenDto). p_para_alumno decide qué datos van:
--    el alumno ve su código de recogida y no ve "alumno"; la cafetería ve al alumno y NUNCA el código
--    (el encargado lo teclea; si lo recibiera, la validación no serviría).
CREATE OR REPLACE FUNCTION fn_cafeteria_abierta(p_id_cafeteria UUID, p_en TIMESTAMPTZ DEFAULT now())
RETURNS BOOLEAN LANGUAGE sql STABLE SET search_path = public, pg_temp AS $$
  SELECT NOT EXISTS (SELECT 1 FROM cierre_cafeteria k
                      WHERE k.id_cafeteria = p_id_cafeteria
                        AND k.fecha = (p_en AT TIME ZONE 'America/Mexico_City')::date)
     AND EXISTS (SELECT 1 FROM horario_cafeteria h
                  WHERE h.id_cafeteria = p_id_cafeteria
                    AND h.dia_semana = EXTRACT(ISODOW FROM p_en AT TIME ZONE 'America/Mexico_City')
                    AND (p_en AT TIME ZONE 'America/Mexico_City')::time >= h.hora_apertura
                    AND (p_en AT TIME ZONE 'America/Mexico_City')::time <  h.hora_cierre);
$$;

CREATE OR REPLACE FUNCTION fn_cafeteria_cierra_a(p_id_cafeteria UUID)
RETURNS TEXT LANGUAGE sql STABLE SET search_path = public, pg_temp AS $$
  SELECT CASE WHEN EXISTS (SELECT 1 FROM cierre_cafeteria k
                            WHERE k.id_cafeteria = p_id_cafeteria
                              AND k.fecha = (now() AT TIME ZONE 'America/Mexico_City')::date)
              THEN NULL
              ELSE to_char(max(h.hora_cierre), 'HH24:MI') END
    FROM horario_cafeteria h
   WHERE h.id_cafeteria = p_id_cafeteria
     AND h.dia_semana = EXTRACT(ISODOW FROM now() AT TIME ZONE 'America/Mexico_City');
$$;

CREATE OR REPLACE FUNCTION fn_orden_json(p_id_orden UUID, p_para_alumno BOOLEAN)
RETURNS JSONB LANGUAGE sql STABLE SET search_path = public, pg_temp AS $$
  SELECT jsonb_build_object(
           'id_orden',           o.id_orden,
           'id_cafeteria',       o.id_cafeteria,
           'cafeteria',          c.nombre,
           'logo_cafeteria',     c.logo_url,
           'punto_entrega',      c.punto_entrega,
           'alumno',             CASE WHEN p_para_alumno THEN NULL
                                      ELSE jsonb_build_object('nombre', u.nombre, 'matricula', u.matricula) END,
           'estado',             o.estado,
           'recogida_en',        o.recogida_en,
           'creado_en',          o.creado_en,
           'codigo_recogida',    CASE WHEN p_para_alumno THEN o.codigo_recogida END,
           'total',              pg.monto,
           'metodo',             pg.metodo,
           'estado_pago',        pg.estado,
           'notas',              o.notas,
           'motivo_cancelacion', o.motivo_cancelacion,
           'lineas', (SELECT COALESCE(jsonb_agg(jsonb_build_object(
                               'id_producto',     d.id_producto,
                               'producto',        p.nombre,
                               'cantidad',        d.cantidad,
                               'precio_unitario', d.precio_unitario,
                               'imagen_url',      p.imagen_url) ORDER BY p.nombre), '[]'::jsonb)
                        FROM detalle_orden d JOIN producto p ON p.id_producto = d.id_producto
                       WHERE d.id_orden = o.id_orden))
    FROM orden o
    JOIN cafeteria c ON c.id_cafeteria = o.id_cafeteria
    JOIN usuario u   ON u.id_usuario = o.id_usuario
    JOIN pago pg     ON pg.id_orden = o.id_orden
   WHERE o.id_orden = p_id_orden;
$$;
