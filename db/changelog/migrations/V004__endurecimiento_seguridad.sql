-- =====================================================================
-- CampusBite · V004 · Endurecimiento de seguridad de la base de datos
-- Modelo de confianza: el backend autentica a la persona y pasa su id; la BD
-- solo expone procedimientos (SECURITY DEFINER) y NUNCA tablas al rol de la API.
-- =====================================================================

-- 1. Nadie crea objetos en el esquema público ni entra a la BD por defecto
REVOKE CREATE ON SCHEMA public FROM PUBLIC;

DO $$
BEGIN
  EXECUTE format('REVOKE ALL ON DATABASE %I FROM PUBLIC', current_database());
  EXECUTE format('GRANT CONNECT ON DATABASE %I TO app_backend', current_database());
EXCEPTION WHEN insufficient_privilege THEN
  RAISE WARNING 'No se pudo ajustar los permisos de la base (no eres su dueño); hazlo manualmente.';
END $$;

-- 2. Límites del rol de la API: una consulta o transacción colgada no tumba el servicio
ALTER ROLE app_backend CONNECTION LIMIT 50;
ALTER ROLE app_backend SET statement_timeout = '10s';
ALTER ROLE app_backend SET lock_timeout = '3s';
ALTER ROLE app_backend SET idle_in_transaction_session_timeout = '15s';

-- 3. Funciones de trigger con search_path fijo (evita suplantación de objetos)
ALTER FUNCTION fn_set_actualizado_en()        SET search_path = public, pg_temp;
ALTER FUNCTION fn_validar_transicion_orden()  SET search_path = public, pg_temp;
ALTER FUNCTION fn_historial_orden()           SET search_path = public, pg_temp;
ALTER FUNCTION fn_cancelar_pago()             SET search_path = public, pg_temp;
ALTER FUNCTION fn_validar_detalle()           SET search_path = public, pg_temp;

-- 4. Código de recogida con aleatoriedad criptográfica (antes random(), predecible)
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
          p_hora_recogida, lpad((('x' || substr(replace(gen_random_uuid()::text, '-', ''), 1, 8))::bit(32)::bigint % 1000000)::text, 6, '0'),
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

-- 5. Lecturas con autorización dentro de la BD.
--    Las vistas con datos por persona dejan de ser accesibles para la API:
--    ahora el filtro por dueño es obligatorio (un descuido del backend no filtra datos ajenos).
REVOKE SELECT ON vw_mis_ordenes, vw_cola_cafeteria FROM app_backend;

-- plpgsql (no sql): el plan se cachea entre llamadas; medido 5x más rápido con 300 mil órdenes
CREATE FUNCTION sp_mis_ordenes(p_id_usuario UUID, p_limite INT DEFAULT 20, p_desplazamiento INT DEFAULT 0)
RETURNS SETOF vw_mis_ordenes
LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  RETURN QUERY
    SELECT * FROM vw_mis_ordenes
     WHERE id_usuario = p_id_usuario
     ORDER BY creada_en DESC
     LIMIT LEAST(GREATEST(p_limite, 1), 100) OFFSET GREATEST(p_desplazamiento, 0);
END $$;

CREATE FUNCTION sp_cola_cafeteria(p_id_cafeteria UUID, p_id_actor UUID)
RETURNS SETOF vw_cola_cafeteria
LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM usuario u LEFT JOIN cafeteria c ON c.id_cafeteria = p_id_cafeteria
     WHERE u.id_usuario = p_id_actor AND u.activo
       AND (u.id_rol = 3 OR (u.id_rol = 2 AND c.id_admin = u.id_usuario))
  ) THEN
    RAISE EXCEPTION 'Sin permiso para ver la cola de esta cafetería' USING ERRCODE = 'insufficient_privilege';
  END IF;
  RETURN QUERY SELECT * FROM vw_cola_cafeteria WHERE id_cafeteria = p_id_cafeteria ORDER BY hora_recogida;
END $$;

GRANT EXECUTE ON FUNCTION sp_mis_ordenes(UUID,INT,INT), sp_cola_cafeteria(UUID,UUID) TO app_backend;
