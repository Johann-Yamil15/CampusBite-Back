-- JY-Sprint4 31/10/2026: marca el correo como verificado con el token del enlace (la API manda su hash).
--   CALL sp_verificar_correo('<sha256 hex del token>', NULL, NULL, NULL);
-- Devuelve el usuario para que la API inicie su sesión. El token se borra: cada enlace sirve una sola vez.
-- Token inexistente, ya usado o de una cuenta inactiva -> check_violation. Vencido -> check_violation con otro mensaje.
CREATE OR REPLACE PROCEDURE sp_verificar_correo(
  IN  p_hash       VARCHAR,
  OUT p_id_usuario UUID,
  OUT p_correo     VARCHAR,
  OUT p_id_rol     SMALLINT
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_expira TIMESTAMPTZ;
BEGIN
  SELECT u.id_usuario, u.correo, u.id_rol, u.verificacion_expira
    INTO p_id_usuario, p_correo, p_id_rol, v_expira
    FROM usuario u
   WHERE u.verificacion_hash = p_hash AND u.activo
     FOR UPDATE;
  IF NOT FOUND THEN
    RAISE EXCEPTION 'El enlace de verificación no es válido o ya se usó' USING ERRCODE = 'check_violation';
  END IF;
  IF v_expira <= now() THEN
    RAISE EXCEPTION 'El enlace de verificación venció; pide que te enviemos otro' USING ERRCODE = 'check_violation';
  END IF;

  PERFORM set_config('app.id_actor', p_id_usuario::text, true);
  UPDATE usuario
     SET correo_verificado = true, verificacion_hash = NULL, verificacion_expira = NULL
   WHERE id_usuario = p_id_usuario;
END $$;
