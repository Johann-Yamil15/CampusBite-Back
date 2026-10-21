-- JY-Sprint2 09/10/2026: interno (la API no lo ejecuta). Falla si el actor no es el encargado de la
-- cafetería ni admin_sistema. Lo usan sp_cambiar_estado_orden, sp_entregar_orden y sp_cola_cafeteria.
CREATE OR REPLACE PROCEDURE sp_exigir_gestor_cafeteria(
  IN p_id_cafeteria UUID,
  IN p_id_actor     UUID,
  IN p_accion       VARCHAR
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM usuario u
     WHERE u.id_usuario = p_id_actor AND u.activo
       AND (u.id_rol = 3 OR (u.id_rol = 2 AND u.id_cafeteria = p_id_cafeteria))
  ) THEN
    RAISE EXCEPTION 'Sin permiso para %', p_accion USING ERRCODE = 'insufficient_privilege';
  END IF;
END $$;
