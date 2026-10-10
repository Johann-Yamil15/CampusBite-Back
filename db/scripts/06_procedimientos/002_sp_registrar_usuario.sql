-- JY-Sprint2 09/10/2026: registro de alumno. Devuelve el id nuevo en p_id_usuario.
--   CALL sp_registrar_usuario('Ana López', 'ana@utt.edu.mx', '<hash bcrypt>', 'UTT123', NULL, NULL);
-- matrícula y teléfono son opcionales: se mandan NULL.
CREATE OR REPLACE PROCEDURE sp_registrar_usuario(
  IN  p_nombre          VARCHAR,
  IN  p_correo          VARCHAR,
  IN  p_contrasena_hash VARCHAR,
  IN  p_matricula       VARCHAR,
  IN  p_telefono        VARCHAR,
  OUT p_id_usuario      UUID
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  INSERT INTO usuario (id_rol, nombre, correo, contrasena_hash, matricula, telefono)
  VALUES (1, btrim(p_nombre), p_correo, p_contrasena_hash, p_matricula, p_telefono)
  RETURNING id_usuario INTO p_id_usuario;
EXCEPTION WHEN unique_violation THEN
  RAISE EXCEPTION 'El correo o la matrícula ya están registrados' USING ERRCODE = 'unique_violation';
END $$;
