-- JJ-Sprint5 13/11/2026: sesión, contraseña, perfil y preferencias del alumno.
--  * sp_obtener_usuario: datos del usuario de la sesión (/me, respuesta de registro-cafeteria, cambiar contraseña).
--      CALL sp_obtener_usuario(<id>, NULL, NULL, NULL, NULL, NULL, NULL);   -- todo NULL si no existe
--  * sp_cambiar_password: el backend ya verificó la actual con BCrypt.
--  * sp_obtener_perfil_alumno / sp_actualizar_perfil_alumno / sp_guardar_preferencias_alumno.
CREATE OR REPLACE PROCEDURE sp_obtener_usuario(
  IN  p_id_usuario      UUID,
  OUT p_correo          VARCHAR,
  OUT p_id_rol          SMALLINT,
  OUT p_nombre          VARCHAR,
  OUT p_id_cafeteria    UUID,
  OUT p_contrasena_hash VARCHAR,
  OUT p_activo          BOOLEAN
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  SELECT u.correo, u.id_rol, u.nombre, u.id_cafeteria, u.contrasena_hash, u.activo
    INTO p_correo, p_id_rol, p_nombre, p_id_cafeteria, p_contrasena_hash, p_activo
    FROM usuario u WHERE u.id_usuario = p_id_usuario;
END $$;

CREATE OR REPLACE PROCEDURE sp_cambiar_password(
  IN p_id_usuario      UUID,
  IN p_contrasena_hash VARCHAR
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  UPDATE usuario SET contrasena_hash = p_contrasena_hash WHERE id_usuario = p_id_usuario AND activo;
  IF NOT FOUND THEN RAISE EXCEPTION 'La sesión no es válida' USING ERRCODE = 'no_data_found'; END IF;
END $$;

-- p_perfil: {"correo", "datos": {"nombre", "matricula", "telefono"},
--            "preferencias": {"id_cafeteria_frecuente", "metodo_pago", "aviso_pedido_listo", "aviso_promociones"}}
CREATE OR REPLACE PROCEDURE sp_obtener_perfil_alumno(
  IN  p_id_usuario UUID,
  OUT p_perfil     JSONB
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  SELECT jsonb_build_object(
           'correo', u.correo,
           'datos', jsonb_build_object('nombre', u.nombre, 'matricula', u.matricula, 'telefono', u.telefono),
           'preferencias', jsonb_build_object(
             'id_cafeteria_frecuente', pa.id_cafeteria_frecuente,
             'metodo_pago',            pa.metodo_pago,
             'aviso_pedido_listo',     COALESCE(pa.aviso_pedido_listo, true),
             'aviso_promociones',      COALESCE(pa.aviso_promociones, false)))
    INTO p_perfil
    FROM usuario u
    LEFT JOIN preferencia_alumno pa ON pa.id_usuario = u.id_usuario
   WHERE u.id_usuario = p_id_usuario AND u.id_rol = 1 AND u.activo;
  IF p_perfil IS NULL THEN RAISE EXCEPTION 'El alumno no existe' USING ERRCODE = 'no_data_found'; END IF;
END $$;

CREATE OR REPLACE PROCEDURE sp_actualizar_perfil_alumno(
  IN p_id_usuario UUID,
  IN p_nombre     VARCHAR,
  IN p_matricula  VARCHAR,
  IN p_telefono   VARCHAR
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  IF EXISTS (SELECT 1 FROM usuario u
              WHERE u.matricula = p_matricula AND u.id_usuario <> p_id_usuario) THEN
    RAISE EXCEPTION 'La matrícula ya está registrada' USING ERRCODE = 'unique_violation';
  END IF;

  UPDATE usuario
     SET nombre = btrim(p_nombre), matricula = NULLIF(btrim(p_matricula), ''), telefono = NULLIF(btrim(p_telefono), '')
   WHERE id_usuario = p_id_usuario AND id_rol = 1 AND activo;
  IF NOT FOUND THEN RAISE EXCEPTION 'El alumno no existe' USING ERRCODE = 'no_data_found'; END IF;
END $$;

CREATE OR REPLACE PROCEDURE sp_guardar_preferencias_alumno(
  IN p_id_usuario       UUID,
  IN p_id_cafeteria     UUID,
  IN p_metodo_pago      metodo_pago,
  IN p_aviso_listo      BOOLEAN,
  IN p_aviso_promociones BOOLEAN
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM usuario u WHERE u.id_usuario = p_id_usuario AND u.id_rol = 1 AND u.activo) THEN
    RAISE EXCEPTION 'El alumno no existe' USING ERRCODE = 'no_data_found';
  END IF;
  IF p_id_cafeteria IS NOT NULL
     AND NOT EXISTS (SELECT 1 FROM cafeteria c WHERE c.id_cafeteria = p_id_cafeteria AND c.activa) THEN
    RAISE EXCEPTION 'La cafetería frecuente no existe o no está activa';
  END IF;

  INSERT INTO preferencia_alumno (id_usuario, id_cafeteria_frecuente, metodo_pago, aviso_pedido_listo, aviso_promociones)
  VALUES (p_id_usuario, p_id_cafeteria, p_metodo_pago, p_aviso_listo, p_aviso_promociones)
  ON CONFLICT (id_usuario) DO UPDATE
     SET id_cafeteria_frecuente = EXCLUDED.id_cafeteria_frecuente,
         metodo_pago            = EXCLUDED.metodo_pago,
         aviso_pedido_listo     = EXCLUDED.aviso_pedido_listo,
         aviso_promociones      = EXCLUDED.aviso_promociones;
END $$;
