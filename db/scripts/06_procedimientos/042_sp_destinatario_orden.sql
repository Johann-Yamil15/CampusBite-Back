-- JY-Sprint5 27/11/2026: a quién se le avisa por correo cuando cambia el estado de una orden.
--   CALL sp_destinatario_orden(<id_orden>, <id_actor>, NULL, NULL, NULL, NULL, NULL);
-- Devuelve el dueño de la orden: id, correo, nombre, su preferencia "avisarme cuando esté lista" (true si nunca la
-- cambió) y el código de recogida (solo para mandárselo a él mismo: la cafetería nunca lo ve en la API).
-- Mismo permiso que sp_obtener_orden: el actor es el dueño de la orden o gestor de su cafetería (si no, 403).
-- Todo NULL si la orden no existe, o si su dueño está inactivo o no verificó su correo (no hay a quién escribirle).
CREATE OR REPLACE PROCEDURE sp_destinatario_orden(
  IN  p_id_orden            UUID,
  IN  p_id_actor            UUID,
  OUT p_id_usuario          UUID,
  OUT p_correo              VARCHAR,
  OUT p_nombre              VARCHAR,
  OUT p_aviso_pedido_listo  BOOLEAN,
  OUT p_codigo_recogida     VARCHAR
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_id_usuario   UUID;
  v_id_cafeteria UUID;
BEGIN
  SELECT o.id_usuario, o.id_cafeteria INTO v_id_usuario, v_id_cafeteria
    FROM orden o WHERE o.id_orden = p_id_orden;
  IF NOT FOUND THEN RETURN; END IF;

  IF v_id_usuario IS DISTINCT FROM p_id_actor THEN
    CALL sp_exigir_gestor_cafeteria(v_id_cafeteria, p_id_actor, 'ver los datos de esta orden');
  END IF;

  SELECT u.id_usuario, u.correo::VARCHAR, u.nombre::VARCHAR, COALESCE(pa.aviso_pedido_listo, true),
         o.codigo_recogida::VARCHAR
    INTO p_id_usuario, p_correo, p_nombre, p_aviso_pedido_listo, p_codigo_recogida
    FROM orden o
    JOIN usuario u ON u.id_usuario = o.id_usuario
    LEFT JOIN preferencia_alumno pa ON pa.id_usuario = u.id_usuario
   WHERE o.id_orden = p_id_orden AND u.activo AND u.correo_verificado;
END $$;
