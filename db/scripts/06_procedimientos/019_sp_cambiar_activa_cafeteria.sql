-- JY-Sprint3 20/10/2026: TEC-08. Baja lógica (activa = false) o reactivación. Solo admin_sistema.
--   CALL sp_cambiar_activa_cafeteria(<id_cafeteria>, false, <id_admin>);
-- No se borra la fila: órdenes, productos y bitácora la siguen necesitando. Una cafetería inactiva no
-- aparece en el menú ni acepta órdenes nuevas.
-- No se puede desactivar con órdenes en curso (pendiente, confirmada, en_preparacion, lista) -> 55006.
CREATE OR REPLACE PROCEDURE sp_cambiar_activa_cafeteria(
  IN p_id_cafeteria UUID,
  IN p_activa       BOOLEAN,
  IN p_id_actor     UUID
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_activa      BOOLEAN;
  v_en_curso    INT;
BEGIN
  CALL sp_exigir_admin_sistema(p_id_actor, CASE WHEN p_activa THEN 'reactivar cafeterías' ELSE 'desactivar cafeterías' END);

  -- FOR UPDATE espera a que terminen las órdenes que se están creando (sp_crear_orden toma FOR SHARE),
  -- así ninguna se cuela entre el conteo y la baja.
  SELECT c.activa INTO v_activa FROM cafeteria c WHERE c.id_cafeteria = p_id_cafeteria FOR UPDATE;
  IF NOT FOUND THEN
    RAISE EXCEPTION 'La cafetería no existe' USING ERRCODE = 'no_data_found';
  END IF;
  IF v_activa = p_activa THEN
    RETURN;                                    -- ya está así: repetir la operación no es un error
  END IF;

  IF NOT p_activa THEN
    SELECT count(*) INTO v_en_curso FROM orden o
     WHERE o.id_cafeteria = p_id_cafeteria AND o.estado IN ('pendiente','confirmada','en_preparacion','lista');
    IF v_en_curso > 0 THEN
      RAISE EXCEPTION 'La cafetería tiene % orden(es) en curso; termínalas o cancélalas antes de desactivarla', v_en_curso
        USING ERRCODE = 'object_in_use';
    END IF;
  END IF;

  PERFORM set_config('app.id_actor', p_id_actor::text, true);
  UPDATE cafeteria SET activa = p_activa WHERE id_cafeteria = p_id_cafeteria;
END $$;
