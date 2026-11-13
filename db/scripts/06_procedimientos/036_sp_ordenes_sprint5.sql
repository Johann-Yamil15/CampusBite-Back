-- JJ-Sprint5 13/11/2026: órdenes del rediseño.
--  * sp_obtener_orden: detalle (seguimiento en vivo). El alumno dueño la ve con su código; el personal de esa
--    cafetería (o admin_sistema) sin código y con el alumno. Cualquier otro: 403 (sp_exigir_gestor_cafeteria).
--      CALL sp_obtener_orden(<id_orden>, <id_actor>, NULL);
--  * sp_confirmar_transferencia: el encargado confirma que recibió el pago (pendiente -> confirmada, pago pagado),
--    con su id en historial_estado_orden. Acepta tarjeta además de transferencia mientras no haya pasarela
--    (decisión pendiente §5.5): sin esto una orden con tarjeta se quedaría "pendiente" para siempre.
--      CALL sp_confirmar_transferencia(<id_orden>, <id_encargado>, 'SPEI 1234');
CREATE OR REPLACE PROCEDURE sp_obtener_orden(
  IN  p_id_orden UUID,
  IN  p_id_actor UUID,
  OUT p_orden    JSONB
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_id_usuario   UUID;
  v_id_cafeteria UUID;
BEGIN
  SELECT o.id_usuario, o.id_cafeteria INTO v_id_usuario, v_id_cafeteria
    FROM orden o WHERE o.id_orden = p_id_orden;
  IF NOT FOUND THEN RAISE EXCEPTION 'La orden no existe' USING ERRCODE = 'no_data_found'; END IF;

  IF v_id_usuario = p_id_actor THEN
    p_orden := fn_orden_json(p_id_orden, true);
    RETURN;
  END IF;

  CALL sp_exigir_gestor_cafeteria(v_id_cafeteria, p_id_actor, 'ver esta orden');
  p_orden := fn_orden_json(p_id_orden, false);
END $$;

CREATE OR REPLACE PROCEDURE sp_confirmar_transferencia(
  IN p_id_orden   UUID,
  IN p_id_actor   UUID,
  IN p_referencia VARCHAR
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_id_cafeteria UUID;
  v_estado_orden estado_orden;
  v_id_pago      UUID;
  v_metodo       metodo_pago;
  v_estado       estado_pago;
BEGIN
  -- Mismo orden de bloqueo que el resto: primero la orden, luego el pago
  SELECT o.id_cafeteria, o.estado INTO v_id_cafeteria, v_estado_orden
    FROM orden o WHERE o.id_orden = p_id_orden FOR UPDATE;
  IF NOT FOUND THEN RAISE EXCEPTION 'La orden no existe' USING ERRCODE = 'no_data_found'; END IF;

  CALL sp_exigir_gestor_cafeteria(v_id_cafeteria, p_id_actor, 'confirmar el pago de esta orden');

  IF v_estado_orden <> 'pendiente' THEN
    RAISE EXCEPTION 'La orden no existe o ya no está pendiente de pago';
  END IF;

  SELECT p.id_pago, p.metodo, p.estado INTO v_id_pago, v_metodo, v_estado
    FROM pago p WHERE p.id_orden = p_id_orden FOR UPDATE;
  IF v_metodo = 'efectivo' OR v_estado <> 'pendiente' THEN
    RAISE EXCEPTION 'El pago no admite confirmación electrónica';
  END IF;

  UPDATE pago SET estado = 'pagado', referencia = NULLIF(btrim(p_referencia), ''), pagado_en = now()
   WHERE id_pago = v_id_pago;
  PERFORM set_config('app.id_actor', p_id_actor::text, true);
  UPDATE orden SET estado = 'confirmada' WHERE id_orden = p_id_orden;
END $$;
