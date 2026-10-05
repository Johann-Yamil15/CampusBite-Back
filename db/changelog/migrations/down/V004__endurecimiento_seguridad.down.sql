-- Rollback de V004
GRANT SELECT ON vw_mis_ordenes, vw_cola_cafeteria TO app_backend;
DROP FUNCTION IF EXISTS sp_cola_cafeteria(UUID, UUID);
DROP FUNCTION IF EXISTS sp_mis_ordenes(UUID, INT, INT);

-- Restaura sp_crear_orden original (V002)
CREATE OR REPLACE FUNCTION sp_crear_orden(
  p_id_usuario UUID, p_id_cafeteria UUID, p_hora_recogida TIMESTAMPTZ,
  p_metodo metodo_pago, p_items JSONB, p_notas VARCHAR DEFAULT NULL
) RETURNS UUID
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_tz       CONSTANT TEXT := 'America/Mexico_City';
  v_local    TIMESTAMP;
  v_n_items  INT; v_n_validos INT; v_cant_ok BOOLEAN;
  v_total    NUMERIC(10,2);
  v_id_orden UUID;
  v_cafe     cafeteria%ROWTYPE;
  v_acepta   BOOLEAN;
BEGIN
  IF NOT EXISTS (SELECT 1 FROM usuario WHERE id_usuario = p_id_usuario AND id_rol = 1 AND activo) THEN
    RAISE EXCEPTION 'Usuario no válido para realizar órdenes';
  END IF;

  SELECT * INTO v_cafe FROM cafeteria WHERE id_cafeteria = p_id_cafeteria AND activa;
  IF NOT FOUND THEN RAISE EXCEPTION 'La cafetería no existe o no está activa'; END IF;

  v_acepta := CASE p_metodo WHEN 'efectivo' THEN v_cafe.acepta_efectivo
                            WHEN 'tarjeta'  THEN v_cafe.acepta_tarjeta
                            ELSE v_cafe.acepta_transferencia END;
  IF NOT v_acepta THEN
    RAISE EXCEPTION 'La cafetería no acepta el método de pago %', p_metodo;
  END IF;

  IF p_hora_recogida < now() + interval '5 minutes' THEN
    RAISE EXCEPTION 'La hora de recogida debe ser al menos 5 minutos en el futuro';
  END IF;
  v_local := p_hora_recogida AT TIME ZONE v_tz;
  IF NOT EXISTS (
    SELECT 1 FROM horario_cafeteria h
     WHERE h.id_cafeteria = p_id_cafeteria
       AND h.dia_semana = EXTRACT(ISODOW FROM v_local)
       AND v_local::time BETWEEN h.hora_apertura AND h.hora_cierre
  ) THEN
    RAISE EXCEPTION 'La cafetería está cerrada a la hora de recogida solicitada';
  END IF;

  IF jsonb_typeof(p_items) <> 'array' OR jsonb_array_length(p_items) = 0 THEN
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
  IF NOT v_cant_ok THEN RAISE EXCEPTION 'La cantidad por producto debe estar entre 1 y 20'; END IF;

  PERFORM set_config('app.id_actor', p_id_usuario::text, true);

  INSERT INTO orden (id_usuario, id_cafeteria, estado, hora_recogida, codigo_recogida, total, notas)
  VALUES (p_id_usuario, p_id_cafeteria,
          CASE WHEN p_metodo = 'efectivo' THEN 'confirmada'::estado_orden
               ELSE 'pendiente'::estado_orden END,
          p_hora_recogida, lpad((floor(random() * 1000000))::int::text, 6, '0'),
          v_total, NULLIF(btrim(p_notas), ''))
  RETURNING id_orden INTO v_id_orden;

  INSERT INTO detalle_orden (id_orden, id_producto, cantidad, precio_unitario)
  SELECT v_id_orden, p.id_producto, i.cantidad, p.precio
    FROM (SELECT (e->>'id_producto')::uuid AS id_producto, SUM((e->>'cantidad')::int) AS cantidad
            FROM jsonb_array_elements(p_items) e GROUP BY 1) i
    JOIN producto p ON p.id_producto = i.id_producto;

  INSERT INTO pago (id_orden, metodo, monto) VALUES (v_id_orden, p_metodo, v_total);
  RETURN v_id_orden;
END $$;

ALTER FUNCTION fn_validar_detalle()           RESET search_path;
ALTER FUNCTION fn_cancelar_pago()             RESET search_path;
ALTER FUNCTION fn_historial_orden()           RESET search_path;
ALTER FUNCTION fn_validar_transicion_orden()  RESET search_path;
ALTER FUNCTION fn_set_actualizado_en()        RESET search_path;

ALTER ROLE app_backend RESET idle_in_transaction_session_timeout;
ALTER ROLE app_backend RESET lock_timeout;
ALTER ROLE app_backend RESET statement_timeout;
ALTER ROLE app_backend CONNECTION LIMIT -1;

DO $$
BEGIN
  EXECUTE format('GRANT CONNECT, TEMPORARY ON DATABASE %I TO PUBLIC', current_database());
EXCEPTION WHEN insufficient_privilege THEN
  RAISE WARNING 'No se pudo restaurar el permiso por defecto de la base.';
END $$;
-- REVOKE CREATE ON SCHEMA public no se revierte a propósito: reabrirlo sería un riesgo.
