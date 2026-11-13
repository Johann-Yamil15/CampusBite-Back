-- JJ-Sprint5 13/11/2026: reportar un problema con un pedido (uno por orden) y estadísticas de la cafetería.
CREATE OR REPLACE PROCEDURE sp_crear_reporte_orden(
  IN  p_id_orden   UUID,
  IN  p_id_usuario UUID,
  IN  p_motivo     motivo_reporte,
  IN  p_detalle    VARCHAR,
  OUT p_folio      VARCHAR
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM orden o WHERE o.id_orden = p_id_orden AND o.id_usuario = p_id_usuario) THEN
    RAISE EXCEPTION 'La orden no existe' USING ERRCODE = 'no_data_found';
  END IF;
  IF EXISTS (SELECT 1 FROM reporte_orden r WHERE r.id_orden = p_id_orden) THEN
    RAISE EXCEPTION 'Ya reportaste un problema con este pedido; lo estamos revisando' USING ERRCODE = 'unique_violation';
  END IF;
  IF p_motivo = 'otro' AND length(btrim(COALESCE(p_detalle, ''))) < 10 THEN
    RAISE EXCEPTION 'Cuéntanos el problema (al menos 10 caracteres)';
  END IF;

  -- shortcut: folio aleatorio de 6 dígitos; un choque (1 en 10^6) responde 409 genérico, reintentar basta
  p_folio := 'R-' || lpad(((('x' || substr(replace(gen_random_uuid()::text, '-', ''), 1, 8))::bit(32)::bigint) % 1000000)::text, 6, '0');
  INSERT INTO reporte_orden (id_orden, motivo, detalle, folio)
  VALUES (p_id_orden, p_motivo, NULLIF(btrim(p_detalle), ''), p_folio);
END $$;

-- p_periodo: 'hoy' | '7d' | '30d' (contando hoy, hora de México). Ventas y pedidos sin canceladas.
CREATE OR REPLACE PROCEDURE sp_estadisticas_cafeteria(
  IN  p_id_cafeteria UUID,
  IN  p_id_actor     UUID,
  IN  p_periodo      VARCHAR,
  OUT p_estadisticas JSONB
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_tz     CONSTANT TEXT := 'America/Mexico_City';
  v_hoy    CONSTANT DATE := (now() AT TIME ZONE 'America/Mexico_City')::date;
  v_dias   CONSTANT TEXT[] := ARRAY['Lun','Mar','Mié','Jue','Vie','Sáb','Dom'];
  v_inicio DATE;
BEGIN
  CALL sp_exigir_gestor_cafeteria(p_id_cafeteria, p_id_actor, 'ver las estadísticas de esta cafetería');
  v_inicio := v_hoy - CASE p_periodo WHEN '7d' THEN 6 WHEN '30d' THEN 29 ELSE 0 END;

  WITH o AS (
    SELECT o.id_orden, o.estado, (o.creado_en AT TIME ZONE v_tz) AS local_en, pg.monto
      FROM orden o JOIN pago pg ON pg.id_orden = o.id_orden
     WHERE o.id_cafeteria = p_id_cafeteria
       AND (o.creado_en AT TIME ZONE v_tz)::date BETWEEN v_inicio AND v_hoy
  ), v AS (SELECT * FROM o WHERE estado <> 'cancelada')
  SELECT jsonb_build_object(
    'ventas',          (SELECT COALESCE(sum(monto), 0) FROM v),
    'pedidos',         (SELECT count(*) FROM v),
    'ticket_promedio', (SELECT COALESCE(round(avg(monto), 2), 0) FROM v),
    'cancelados',      (SELECT count(*) FROM o WHERE estado = 'cancelada'),
    'por_estado', jsonb_build_object(
      'nuevos',      (SELECT count(*) FROM o WHERE estado IN ('pendiente','confirmada')),
      'preparacion', (SELECT count(*) FROM o WHERE estado = 'en_preparacion'),
      'listos',      (SELECT count(*) FROM o WHERE estado = 'lista'),
      'entregados',  (SELECT count(*) FROM o WHERE estado = 'entregada'),
      'cancelados',  (SELECT count(*) FROM o WHERE estado = 'cancelada')),
    'ventas_por_dia', (SELECT jsonb_agg(jsonb_build_object(
                         'etiqueta', v_dias[EXTRACT(ISODOW FROM g.d)::int],
                         'valor',    (SELECT COALESCE(sum(monto), 0) FROM v WHERE local_en::date = g.d)) ORDER BY g.d)
                         FROM generate_series(v_inicio, v_hoy, interval '1 day') g(d)),
    'mas_vendidos', (SELECT COALESCE(jsonb_agg(x ORDER BY x.piezas DESC, x.producto), '[]'::jsonb) FROM (
                       SELECT p.nombre AS producto, sum(d.cantidad)::int AS piezas, sum(d.subtotal) AS monto
                         FROM v JOIN detalle_orden d ON d.id_orden = v.id_orden
                         JOIN producto p ON p.id_producto = d.id_producto
                        GROUP BY p.nombre ORDER BY 2 DESC, 1 LIMIT 5) x),
    'horas_pico', (SELECT COALESCE(jsonb_agg(jsonb_build_object('etiqueta', lpad(h::text, 2, '0') || ':00', 'valor', n)
                                             ORDER BY h), '[]'::jsonb)
                     FROM (SELECT EXTRACT(HOUR FROM local_en)::int AS h, count(*) AS n FROM v GROUP BY 1) y))
    INTO p_estadisticas;
END $$;
