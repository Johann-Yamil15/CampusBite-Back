-- JY-Sprint2 09/10/2026: datos para el login (el hash se compara en el backend con BCrypt).
--   CALL sp_obtener_credenciales('ana@utt.edu.mx', NULL, NULL, NULL, NULL);
-- Si el correo no existe, los cuatro valores regresan NULL.
CREATE OR REPLACE PROCEDURE sp_obtener_credenciales(
  IN  p_correo          VARCHAR,
  OUT p_id_usuario      UUID,
  OUT p_id_rol          SMALLINT,
  OUT p_contrasena_hash VARCHAR,
  OUT p_activo          BOOLEAN
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  SELECT u.id_usuario, u.id_rol, u.contrasena_hash, u.activo
    INTO p_id_usuario, p_id_rol, p_contrasena_hash, p_activo
    FROM usuario u
   WHERE u.correo = p_correo::citext;
END $$;
