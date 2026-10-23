-- JY-Sprint2 09/10/2026: el alumno cancela su orden (solo antes de que entre a preparación)
--   CALL sp_cancelar_orden(<id_orden>, <id_alumno>);
CREATE OR REPLACE PROCEDURE sp_cancelar_orden(
  IN p_id_orden   UUID,
  IN p_id_usuario UUID
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_estado estado_orden;
BEGIN
  SELECT o.estado INTO v_estado FROM orden o
   WHERE o.id_orden = p_id_orden AND o.id_usuario = p_id_usuario FOR UPDATE;
  IF NOT FOUND THEN RAISE EXCEPTION 'La orden no existe'; END IF;
  IF v_estado NOT IN ('pendiente','confirmada') THEN
    RAISE EXCEPTION 'La orden ya está en preparación y no se puede cancelar';
  END IF;

  PERFORM set_config('app.id_actor', p_id_usuario::text, true);
  UPDATE orden SET estado = 'cancelada' WHERE id_orden = p_id_orden;
END $$;
