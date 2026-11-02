-- JY-Sprint4 31/10/2026: guarda el token de verificación de correo (su hash) para un usuario sin verificar.
-- Lo usa la API al registrarse y al pedir que se reenvíe el correo.
--   CALL sp_generar_verificacion('ana@utt.edu.mx', '<sha256 hex del token>', now() + interval '24 hours', NULL);
-- p_nombre: el nombre para saludar en el correo, o NULL si el correo no existe, la cuenta está inactiva o ya está
-- verificada. En ese caso la API no manda nada ni lo dice (no revela qué correos están registrados).
-- Un token nuevo reemplaza al anterior: solo sirve el enlace del último correo.
CREATE OR REPLACE PROCEDURE sp_generar_verificacion(
  IN  p_correo VARCHAR,
  IN  p_hash   VARCHAR,
  IN  p_expira TIMESTAMPTZ,
  OUT p_nombre VARCHAR
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_id UUID;
BEGIN
  IF p_hash IS NULL OR p_hash !~ '^[0-9a-f]{64}$' THEN
    RAISE EXCEPTION 'El hash del token no es válido' USING ERRCODE = 'check_violation';
  END IF;
  IF p_expira IS NULL OR p_expira <= now() THEN
    RAISE EXCEPTION 'La expiración del token debe ser futura' USING ERRCODE = 'check_violation';
  END IF;

  SELECT u.id_usuario, u.nombre INTO v_id, p_nombre
    FROM usuario u
   WHERE u.correo = p_correo::citext AND u.activo AND NOT u.correo_verificado
     FOR UPDATE;
  IF NOT FOUND THEN
    RETURN;
  END IF;

  PERFORM set_config('app.id_actor', v_id::text, true);       -- autor en la bitácora: el propio usuario
  UPDATE usuario SET verificacion_hash = p_hash, verificacion_expira = p_expira WHERE id_usuario = v_id;
END $$;
