-- JY-Sprint2 09/10/2026: datos para el login (el hash se compara en el backend con BCrypt).
--   CALL sp_obtener_credenciales('ana@utt.edu.mx', NULL, NULL, NULL, NULL, NULL, NULL, NULL);
-- Si el correo no existe, todos los valores regresan NULL.
-- JY-Sprint4 31/10/2026: p_correo_verificado; sin verificar no se puede iniciar sesión (lo decide la API después
-- de comprobar la contraseña, para no revelar qué correos existen). Se quita la firma anterior.
-- JJ-Sprint5 13/11/2026: también devuelve nombre e id_cafeteria (el front ya no adivina la cafetería del encargado).
DROP PROCEDURE IF EXISTS sp_obtener_credenciales(VARCHAR, UUID, SMALLINT, VARCHAR, BOOLEAN);
DROP PROCEDURE IF EXISTS sp_obtener_credenciales(VARCHAR, UUID, SMALLINT, VARCHAR, BOOLEAN, BOOLEAN);
-- Versión intermedia del Sprint 5 (nombre e id_cafeteria sin correo_verificado), solo en bases locales de prueba
DROP PROCEDURE IF EXISTS sp_obtener_credenciales(VARCHAR, UUID, SMALLINT, VARCHAR, BOOLEAN, VARCHAR, UUID);

CREATE OR REPLACE PROCEDURE sp_obtener_credenciales(
  IN  p_correo            VARCHAR,
  OUT p_id_usuario        UUID,
  OUT p_id_rol            SMALLINT,
  OUT p_contrasena_hash   VARCHAR,
  OUT p_activo            BOOLEAN,
  OUT p_correo_verificado BOOLEAN,
  OUT p_nombre            VARCHAR,
  OUT p_id_cafeteria      UUID
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  SELECT u.id_usuario, u.id_rol, u.contrasena_hash, u.activo, u.correo_verificado, u.nombre, u.id_cafeteria
    INTO p_id_usuario, p_id_rol, p_contrasena_hash, p_activo, p_correo_verificado, p_nombre, p_id_cafeteria
    FROM usuario u
   WHERE u.correo = p_correo::citext;
END $$;
