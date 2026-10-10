-- JY-Sprint2 09/10/2026: crea la orden completa (orden + detalle + pago) en una transacción.
--   CALL sp_crear_orden(<id_usuario>, <id_cafeteria>, '2026-10-10 13:00-06', 'efectivo',
--                       '[{"id_producto":"<uuid>","cantidad":2}]', 'Sin cebolla', NULL);
-- Devuelve el id en p_id_orden. Efectivo nace "confirmada"; tarjeta y transferencia, "pendiente".
-- p_notas es opcional: se manda NULL.
CREATE OR REPLACE PROCEDURE sp_crear_orden(
  IN  p_id_usuario   UUID,
  IN  p_id_cafeteria UUID,
  IN  p_recogida_en  TIMESTAMPTZ,
  IN  p_metodo       metodo_pago,
  IN  p_items        JSONB,
  IN  p_notas        VARCHAR,
  OUT p_id_orden     UUID
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_tz        CONSTANT TEXT := 'America/Mexico_City';   -- zona de horario_cafeteria
  v_local     TIMESTAMP;
  v_n_items   INT;
  v_n_validos INT;
  v_cant_ok   BOOLEAN;
  v_total     NUMERIC(10,2);
BEGIN
  IF NOT EXISTS (SELECT 1 FROM usuario u
                  WHERE u.id_usuario = p_id_usuario AND u.id_rol = 1 AND u.activo) THEN
    RAISE EXCEPTION 'Usuario no válido para realizar órdenes';
  END IF;

  IF NOT EXISTS (SELECT 1 FROM cafeteria c WHERE c.id_cafeteria = p_id_cafeteria AND c.activa) THEN
    RAISE EXCEPTION 'La cafetería no existe o no está activa';
  END IF;

  IF NOT EXISTS (SELECT 1 FROM cafeteria_metodo_pago m
                  WHERE m.id_cafeteria = p_id_cafeteria AND m.metodo = p_metodo) THEN
    RAISE EXCEPTION 'La cafetería no acepta el método de pago %', p_metodo;
  END IF;

  IF p_recogida_en < now() + interval '5 minutes' THEN
    RAISE EXCEPTION 'La hora de recogida debe ser al menos 5 minutos en el futuro';
  END IF;
  v_local := p_recogida_en AT TIME ZONE v_tz;
  IF NOT EXISTS (
    SELECT 1 FROM horario_cafeteria h
     WHERE h.id_cafeteria = p_id_cafeteria
       AND h.dia_semana = EXTRACT(ISODOW FROM v_local)
       AND v_local::time BETWEEN h.hora_apertura AND h.hora_cierre
  ) THEN
    RAISE EXCEPTION 'La cafetería está cerrada a la hora de recogida solicitada';
  END IF;

  -- Dos IF separados: jsonb_array_length falla si p_items no es un arreglo.
  IF p_items IS NULL OR jsonb_typeof(p_items) <> 'array' THEN
    RAISE EXCEPTION 'La orden debe incluir al menos un producto';
  END IF;
  IF jsonb_array_length(p_items) = 0 THEN
    RAISE EXCEPTION 'La orden debe incluir al menos un producto';
  END IF;

  WITH items AS (
    SELECT (e->>'id_producto')::uuid AS id_producto, SUM((e->>'cantidad')::int) AS cantidad
      FROM jsonb_array_elements(p_items) e GROUP BY 1
  )
  SELECT count(*), count(p.id_producto), COALESCE(bool_and(i.cantidad BETWEEN 1 AND 20), false),
         SUM(i.cantidad * p.precio)
    INTO v_n_items, v_n_validos, v_cant_ok, v_total
    FROM items i
    LEFT JOIN producto p ON p.id_producto = i.id_producto
                        AND p.id_cafeteria = p_id_cafeteria AND p.disponible;

  IF v_n_items <> v_n_validos THEN
    RAISE EXCEPTION 'Hay productos inexistentes, no disponibles o de otra cafetería';
  END IF;
  IF NOT v_cant_ok THEN
    RAISE EXCEPTION 'La cantidad por producto debe estar entre 1 y 20';
  END IF;

  PERFORM set_config('app.id_actor', p_id_usuario::text, true);

  -- Código de recogida con aleatoriedad criptográfica (gen_random_uuid), no con random().
  INSERT INTO orden (id_usuario, id_cafeteria, estado, recogida_en, codigo_recogida, notas)
  VALUES (p_id_usuario, p_id_cafeteria,
          CASE WHEN p_metodo = 'efectivo' THEN 'confirmada'::estado_orden
               ELSE 'pendiente'::estado_orden END,
          p_recogida_en,
          lpad((('x' || substr(replace(gen_random_uuid()::text, '-', ''), 1, 8))::bit(32)::bigint % 1000000)::text, 6, '0'),
          NULLIF(btrim(p_notas), ''))
  RETURNING id_orden INTO p_id_orden;

  INSERT INTO detalle_orden (id_orden, id_producto, cantidad, precio_unitario)
  SELECT p_id_orden, p.id_producto, i.cantidad, p.precio
    FROM (SELECT (e->>'id_producto')::uuid AS id_producto, SUM((e->>'cantidad')::int) AS cantidad
            FROM jsonb_array_elements(p_items) e GROUP BY 1) i
    JOIN producto p ON p.id_producto = i.id_producto;

  INSERT INTO pago (id_orden, metodo, monto) VALUES (p_id_orden, p_metodo, v_total);
END $$;
