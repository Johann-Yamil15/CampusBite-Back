-- JY-Sprint2 10/10/2026: historial completo de un registro (altas, cambios y bajas), del más reciente al más
-- antiguo. Solo admin_sistema: la bitácora muestra datos de todas las personas.
--   CALL sp_bitacora_registro(<id_admin>, 'orden', '<id_orden>', 50, NULL);
-- p_historial: [{"operacion": "I|U|D", "datos_antes", "datos_despues", "cambiado_por", "cambiado_en"}]
CREATE OR REPLACE PROCEDURE sp_bitacora_registro(
  IN  p_id_actor    UUID,
  IN  p_tabla       VARCHAR,
  IN  p_id_registro VARCHAR,
  IN  p_limite      INT,
  OUT p_historial   JSONB
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM usuario u WHERE u.id_usuario = p_id_actor AND u.activo AND u.id_rol = 3) THEN
    RAISE EXCEPTION 'Sin permiso para ver la bitácora' USING ERRCODE = 'insufficient_privilege';
  END IF;

  SELECT COALESCE(jsonb_agg(to_jsonb(h) ORDER BY h.cambiado_en DESC, h.id_bitacora DESC), '[]'::jsonb)
    INTO p_historial
    FROM (SELECT b.id_bitacora, b.operacion, b.datos_antes, b.datos_despues, b.cambiado_por, b.cambiado_en
            FROM bitacora b
           WHERE b.tabla = p_tabla AND b.id_registro = p_id_registro
           ORDER BY b.cambiado_en DESC, b.id_bitacora DESC
           LIMIT LEAST(GREATEST(COALESCE(p_limite, 50), 1), 500)) h;
END $$;
