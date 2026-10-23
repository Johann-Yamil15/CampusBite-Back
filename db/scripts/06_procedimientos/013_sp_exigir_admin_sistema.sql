-- JY-Sprint3 20/10/2026: interno (la API no lo ejecuta). Falla si el actor no es admin_sistema activo.
-- El rol se lee de la BD, no del token: si a alguien le quitaron el rol, aquí ya no pasa.
CREATE OR REPLACE PROCEDURE sp_exigir_admin_sistema(
  IN p_id_actor UUID,
  IN p_accion   VARCHAR
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM usuario u WHERE u.id_usuario = p_id_actor AND u.activo AND u.id_rol = 3) THEN
    RAISE EXCEPTION 'Sin permiso para %', p_accion USING ERRCODE = 'insufficient_privilege';
  END IF;
END $$;
