-- JY-Sprint2 09/10/2026: el alumno cancela su orden (solo antes de que entre a preparación)
--   CALL sp_cancelar_orden(<id_orden>, <id_alumno>);
-- JJ-Sprint5 13/11/2026: guarda el motivo fijo "Cancelado por el alumno"; una orden ajena es 404 (no se revela).
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
  IF NOT FOUND THEN RAISE EXCEPTION 'La orden no existe' USING ERRCODE = 'no_data_found'; END IF;
  IF v_estado NOT IN ('pendiente','confirmada') THEN
    RAISE EXCEPTION 'La orden ya está en preparación y no se puede cancelar';
  END IF;

  PERFORM set_config('app.id_actor', p_id_usuario::text, true);
  UPDATE orden SET estado = 'cancelada', motivo_cancelacion = 'Cancelado por el alumno'
   WHERE id_orden = p_id_orden;
END $$;
