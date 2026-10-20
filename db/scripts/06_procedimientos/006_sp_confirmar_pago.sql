-- JY-Sprint2 09/10/2026: confirma el pago con tarjeta o transferencia (lo llama el backend tras la pasarela)
--   CALL sp_confirmar_pago(<id_orden>, 'REF-PASARELA-123');
CREATE OR REPLACE PROCEDURE sp_confirmar_pago(
  IN p_id_orden   UUID,
  IN p_referencia VARCHAR
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_estado_orden estado_orden;
  v_id_pago      UUID;
  v_metodo       metodo_pago;
  v_estado       estado_pago;
BEGIN
  -- Mismo orden de bloqueo que el resto de procedimientos: primero la orden, luego el pago.
  -- Si cada uno bloqueara en distinto orden, un "cancelar" y un "confirmar" simultáneos se esperarían
  -- mutuamente (deadlock).
  SELECT o.estado INTO v_estado_orden FROM orden o WHERE o.id_orden = p_id_orden FOR UPDATE;
  IF NOT FOUND OR v_estado_orden <> 'pendiente' THEN
    RAISE EXCEPTION 'La orden no existe o ya no está pendiente de pago';
  END IF;

  SELECT p.id_pago, p.metodo, p.estado INTO v_id_pago, v_metodo, v_estado
    FROM pago p WHERE p.id_orden = p_id_orden FOR UPDATE;
  IF v_metodo = 'efectivo' OR v_estado <> 'pendiente' THEN
    RAISE EXCEPTION 'El pago no admite confirmación electrónica';
  END IF;

  UPDATE pago SET estado = 'pagado', referencia = p_referencia, pagado_en = now()
   WHERE id_pago = v_id_pago;
  PERFORM set_config('app.id_actor', '', true);    -- lo confirma el sistema, no una persona
  UPDATE orden SET estado = 'confirmada' WHERE id_orden = p_id_orden;
END $$;
