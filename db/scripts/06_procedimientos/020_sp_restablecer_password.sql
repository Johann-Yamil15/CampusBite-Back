-- JJ-Sprint3 21/10/2026: TEC-12. Recuperación de contraseña, versión simple (sin código de verificación
-- por correo; se agrega en una historia futura). Pensado para un formulario público (sin sesión).
-- A propósito NO expone si el correo existe: si no hay una fila que actualizar, no hace nada y no falla,
-- igual que sp_obtener_credenciales en el login (no revelar qué correos están registrados).
--   CALL sp_restablecer_password('ana@uttt.edu.mx', '<hash bcrypt nuevo>');
CREATE OR REPLACE PROCEDURE sp_restablecer_password(
  IN p_correo             VARCHAR,
  IN p_contrasena_hash    VARCHAR
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  UPDATE usuario
     SET contrasena_hash = p_contrasena_hash
   WHERE correo = p_correo
     AND activo;
END $$;
