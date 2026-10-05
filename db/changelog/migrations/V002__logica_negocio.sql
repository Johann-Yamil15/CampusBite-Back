-- =====================================================================
-- CampusBite · TEC-03 · Migración V002: lógica de negocio
-- Triggers (auditoría, máquina de estados, historial, pagos) y procedimientos almacenados.
-- Origen: campusbite_TEC-02.sql (PostgreSQL 14+). Se aplica en una sola
-- transacción por Liquibase (ver changelog-master.xml).
-- =====================================================================

-- 3. Triggers ------------------------------------------------------------
-- 3.1 Mantiene actualizado_en
CREATE FUNCTION fn_set_actualizado_en() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
  NEW.actualizado_en := now();
  RETURN NEW;
END $$;

CREATE TRIGGER trg_usuario_upd   BEFORE UPDATE ON usuario   FOR EACH ROW EXECUTE FUNCTION fn_set_actualizado_en();
CREATE TRIGGER trg_cafeteria_upd BEFORE UPDATE ON cafeteria FOR EACH ROW EXECUTE FUNCTION fn_set_actualizado_en();
CREATE TRIGGER trg_producto_upd  BEFORE UPDATE ON producto  FOR EACH ROW EXECUTE FUNCTION fn_set_actualizado_en();
CREATE TRIGGER trg_orden_upd     BEFORE UPDATE ON orden     FOR EACH ROW EXECUTE FUNCTION fn_set_actualizado_en();
CREATE TRIGGER trg_pago_upd      BEFORE UPDATE ON pago      FOR EACH ROW EXECUTE FUNCTION fn_set_actualizado_en();

-- 3.2 Máquina de estados de la orden
CREATE FUNCTION fn_validar_transicion_orden() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
  IF NEW.estado = OLD.estado THEN RETURN NEW; END IF;
  IF NOT (
       (OLD.estado = 'pendiente'      AND NEW.estado IN ('confirmada','cancelada'))
    OR (OLD.estado = 'confirmada'     AND NEW.estado IN ('en_preparacion','cancelada'))
    OR (OLD.estado = 'en_preparacion' AND NEW.estado IN ('lista','cancelada'))
    OR (OLD.estado = 'lista'          AND NEW.estado IN ('entregada','cancelada'))
  ) THEN
    RAISE EXCEPTION 'Transición de estado no permitida: % -> %', OLD.estado, NEW.estado
      USING ERRCODE = 'check_violation';
  END IF;
  RETURN NEW;
END $$;

CREATE TRIGGER trg_orden_transicion BEFORE UPDATE OF estado ON orden
  FOR EACH ROW EXECUTE FUNCTION fn_validar_transicion_orden();

-- 3.3 Bitácora automática de estados (el actor llega vía app.id_actor)
CREATE FUNCTION fn_historial_orden() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
  INSERT INTO historial_estado_orden (id_orden, estado, cambiado_por)
  VALUES (NEW.id_orden, NEW.estado,
          NULLIF(current_setting('app.id_actor', true), '')::uuid);
  RETURN NEW;
END $$;

CREATE TRIGGER trg_orden_historial_ins AFTER INSERT ON orden
  FOR EACH ROW EXECUTE FUNCTION fn_historial_orden();
CREATE TRIGGER trg_orden_historial_upd AFTER UPDATE OF estado ON orden
  FOR EACH ROW WHEN (OLD.estado IS DISTINCT FROM NEW.estado)
  EXECUTE FUNCTION fn_historial_orden();

-- 3.4 Al cancelar una orden se cierra su pago
CREATE FUNCTION fn_cancelar_pago() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
  UPDATE pago
     SET estado    = CASE WHEN estado = 'pagado' THEN 'reembolsado'::estado_pago
                          ELSE 'cancelado'::estado_pago END,
         pagado_en = CASE WHEN estado = 'pagado' THEN pagado_en ELSE NULL END
   WHERE id_orden = NEW.id_orden AND estado IN ('pendiente','pagado');
  RETURN NEW;
END $$;

CREATE TRIGGER trg_orden_cancelada AFTER UPDATE OF estado ON orden
  FOR EACH ROW WHEN (NEW.estado = 'cancelada' AND OLD.estado <> 'cancelada')
  EXECUTE FUNCTION fn_cancelar_pago();

-- 3.5 Un detalle solo puede llevar productos de la cafetería de su orden
CREATE FUNCTION fn_validar_detalle() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
  IF (SELECT p.id_cafeteria FROM producto p WHERE p.id_producto = NEW.id_producto)
     IS DISTINCT FROM
     (SELECT o.id_cafeteria FROM orden o WHERE o.id_orden = NEW.id_orden) THEN
    RAISE EXCEPTION 'El producto no pertenece a la cafetería de la orden'
      USING ERRCODE = 'check_violation';
  END IF;
  RETURN NEW;
END $$;

CREATE TRIGGER trg_detalle_valida BEFORE INSERT OR UPDATE ON detalle_orden
  FOR EACH ROW EXECUTE FUNCTION fn_validar_detalle();

-- 4. Procedimientos almacenados -----------------------------------------
-- 4.1 Registro de alumno
CREATE FUNCTION sp_registrar_usuario(
  p_nombre VARCHAR, p_correo VARCHAR, p_contrasena_hash VARCHAR,
  p_matricula VARCHAR DEFAULT NULL, p_telefono VARCHAR DEFAULT NULL
) RETURNS UUID
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE v_id UUID;
BEGIN
  INSERT INTO usuario (id_rol, nombre, correo, contrasena_hash, matricula, telefono)
  VALUES (1, btrim(p_nombre), p_correo, p_contrasena_hash, p_matricula, p_telefono)
  RETURNING id_usuario INTO v_id;
  RETURN v_id;
EXCEPTION WHEN unique_violation THEN
  RAISE EXCEPTION 'El correo o la matrícula ya están registrados' USING ERRCODE = 'unique_violation';
END $$;

-- 4.2 Credenciales para el login (el hash se compara en el backend)
CREATE FUNCTION sp_obtener_credenciales(p_correo VARCHAR)
RETURNS TABLE (id_usuario UUID, id_rol SMALLINT, contrasena_hash VARCHAR, activo BOOLEAN)
LANGUAGE sql STABLE SECURITY DEFINER SET search_path = public, pg_temp AS $$
  SELECT u.id_usuario, u.id_rol, u.contrasena_hash, u.activo
    FROM usuario u WHERE u.correo = p_correo::citext;
$$;

-- 4.3 Crear orden completa (orden + detalles + pago) en una transacción
--     p_items: [{"id_producto":"<uuid>","cantidad":2}, ...]
CREATE FUNCTION sp_crear_orden(
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

-- 4.4 Confirmar pago de tarjeta/transferencia (lo llama el backend tras la pasarela)
CREATE FUNCTION sp_confirmar_pago(p_id_orden UUID, p_referencia VARCHAR) RETURNS VOID
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE v_pago pago%ROWTYPE;
BEGIN
  SELECT p.* INTO v_pago FROM pago p JOIN orden o USING (id_orden)
   WHERE p.id_orden = p_id_orden AND o.estado = 'pendiente' FOR UPDATE OF p, o;
  IF NOT FOUND THEN RAISE EXCEPTION 'La orden no existe o ya no está pendiente de pago'; END IF;
  IF v_pago.metodo = 'efectivo' OR v_pago.estado <> 'pendiente' THEN
    RAISE EXCEPTION 'El pago no admite confirmación electrónica';
  END IF;

  UPDATE pago SET estado = 'pagado', referencia = p_referencia, pagado_en = now()
   WHERE id_pago = v_pago.id_pago;
  PERFORM set_config('app.id_actor', '', true);
  UPDATE orden SET estado = 'confirmada' WHERE id_orden = p_id_orden;
END $$;

-- 4.5 La cafetería avanza la orden: en_preparacion | lista | cancelada
CREATE FUNCTION sp_cambiar_estado_orden(p_id_orden UUID, p_nuevo estado_orden, p_id_actor UUID)
RETURNS VOID
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE v_cafe UUID;
BEGIN
  IF p_nuevo NOT IN ('en_preparacion','lista','cancelada') THEN
    RAISE EXCEPTION 'Estado no permitido en este procedimiento: %', p_nuevo;
  END IF;
  SELECT o.id_cafeteria INTO v_cafe FROM orden o WHERE o.id_orden = p_id_orden FOR UPDATE;
  IF NOT FOUND THEN RAISE EXCEPTION 'La orden no existe'; END IF;
  IF NOT EXISTS (
    SELECT 1 FROM usuario u LEFT JOIN cafeteria c ON c.id_cafeteria = v_cafe
     WHERE u.id_usuario = p_id_actor AND u.activo
       AND (u.id_rol = 3 OR (u.id_rol = 2 AND c.id_admin = u.id_usuario))
  ) THEN RAISE EXCEPTION 'Sin permiso para modificar esta orden' USING ERRCODE = 'insufficient_privilege'; END IF;

  PERFORM set_config('app.id_actor', p_id_actor::text, true);
  UPDATE orden SET estado = p_nuevo WHERE id_orden = p_id_orden;
END $$;

-- 4.6 El alumno cancela su orden (solo antes de que se prepare)
CREATE FUNCTION sp_cancelar_orden(p_id_orden UUID, p_id_usuario UUID) RETURNS VOID
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE v_estado estado_orden;
BEGIN
  SELECT estado INTO v_estado FROM orden
   WHERE id_orden = p_id_orden AND id_usuario = p_id_usuario FOR UPDATE;
  IF NOT FOUND THEN RAISE EXCEPTION 'La orden no existe'; END IF;
  IF v_estado NOT IN ('pendiente','confirmada') THEN
    RAISE EXCEPTION 'La orden ya está en preparación y no se puede cancelar';
  END IF;
  PERFORM set_config('app.id_actor', p_id_usuario::text, true);
  UPDATE orden SET estado = 'cancelada' WHERE id_orden = p_id_orden;
END $$;

-- 4.7 Entrega: valida el código del alumno; el efectivo se cobra aquí
CREATE FUNCTION sp_entregar_orden(p_id_orden UUID, p_codigo VARCHAR, p_id_actor UUID)
RETURNS VOID
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE v_o orden%ROWTYPE;
BEGIN
  SELECT * INTO v_o FROM orden WHERE id_orden = p_id_orden FOR UPDATE;
  IF NOT FOUND THEN RAISE EXCEPTION 'La orden no existe'; END IF;
  IF NOT EXISTS (
    SELECT 1 FROM usuario u LEFT JOIN cafeteria c ON c.id_cafeteria = v_o.id_cafeteria
     WHERE u.id_usuario = p_id_actor AND u.activo
       AND (u.id_rol = 3 OR (u.id_rol = 2 AND c.id_admin = u.id_usuario))
  ) THEN RAISE EXCEPTION 'Sin permiso para entregar esta orden' USING ERRCODE = 'insufficient_privilege'; END IF;
  IF v_o.estado <> 'lista' THEN RAISE EXCEPTION 'La orden no está lista para entrega'; END IF;
  IF v_o.codigo_recogida <> p_codigo THEN RAISE EXCEPTION 'Código de recogida incorrecto'; END IF;

  UPDATE pago SET estado = 'pagado', pagado_en = now()
   WHERE id_orden = p_id_orden AND metodo = 'efectivo' AND estado = 'pendiente';
  PERFORM set_config('app.id_actor', p_id_actor::text, true);
  UPDATE orden SET estado = 'entregada' WHERE id_orden = p_id_orden;
END $$;
