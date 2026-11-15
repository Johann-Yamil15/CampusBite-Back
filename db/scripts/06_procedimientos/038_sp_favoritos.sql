-- JJ-Sprint5 13/11/2026: favoritos del alumno (cafeterías y platillos). Guardar y quitar son idempotentes.
-- Un platillo retirado o no disponible sigue en la lista con disponible = false y su último nombre/precio.
CREATE OR REPLACE FUNCTION fn_favorito_cafeteria_json(p_id_cafeteria UUID)
RETURNS JSONB LANGUAGE sql STABLE SET search_path = public, pg_temp AS $$
  SELECT jsonb_build_object(
           'id_cafeteria',  c.id_cafeteria,
           'nombre',        c.nombre,
           'foto',          c.portada_url,
           'calificacion',  NULL,
           'punto_entrega', c.punto_entrega,
           'es_del_campus', c.es_del_campus,
           'abierta',       c.activa AND fn_cafeteria_abierta(c.id_cafeteria),
           'platillos',     (SELECT count(*) FROM producto p WHERE p.id_cafeteria = c.id_cafeteria AND p.disponible))
    FROM cafeteria c WHERE c.id_cafeteria = p_id_cafeteria;
$$;

CREATE OR REPLACE FUNCTION fn_favorito_producto_json(p_id_usuario UUID, p_id_producto UUID)
RETURNS JSONB LANGUAGE sql STABLE SET search_path = public, pg_temp AS $$
  SELECT jsonb_build_object(
           'id_producto',       f.id_producto,
           'producto',          COALESCE(p.nombre, f.nombre_guardado),
           'precio',            COALESCE(p.precio, f.precio_guardado),
           'imagen_url',        CASE WHEN p.id_producto IS NULL THEN f.imagen_guardada ELSE p.imagen_url END,
           'id_cafeteria',      f.id_cafeteria,
           'cafeteria',         c.nombre,
           'disponible',        COALESCE(p.disponible AND c.activa, false),
           'cafeteria_abierta', c.activa AND fn_cafeteria_abierta(c.id_cafeteria))
    FROM favorito_producto f
    JOIN cafeteria c      ON c.id_cafeteria = f.id_cafeteria
    LEFT JOIN producto p  ON p.id_producto = f.id_producto
   WHERE f.id_usuario = p_id_usuario AND f.id_producto = p_id_producto;
$$;

CREATE OR REPLACE PROCEDURE sp_favoritos_cafeterias(IN p_id_usuario UUID, OUT p_lista JSONB)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  SELECT COALESCE(jsonb_agg(fn_favorito_cafeteria_json(f.id_cafeteria) ORDER BY f.creado_en DESC), '[]'::jsonb)
    INTO p_lista
    FROM favorito_cafeteria f JOIN cafeteria c ON c.id_cafeteria = f.id_cafeteria
   WHERE f.id_usuario = p_id_usuario AND c.activa;
END $$;

CREATE OR REPLACE PROCEDURE sp_guardar_favorito_cafeteria(IN p_id_usuario UUID, IN p_id_cafeteria UUID, OUT p_favorito JSONB)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM cafeteria c WHERE c.id_cafeteria = p_id_cafeteria AND c.activa) THEN
    RAISE EXCEPTION 'La cafetería no existe o no está disponible' USING ERRCODE = 'no_data_found';
  END IF;
  INSERT INTO favorito_cafeteria (id_usuario, id_cafeteria) VALUES (p_id_usuario, p_id_cafeteria)
  ON CONFLICT DO NOTHING;
  p_favorito := fn_favorito_cafeteria_json(p_id_cafeteria);
END $$;

CREATE OR REPLACE PROCEDURE sp_quitar_favorito_cafeteria(IN p_id_usuario UUID, IN p_id_cafeteria UUID)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  DELETE FROM favorito_cafeteria WHERE id_usuario = p_id_usuario AND id_cafeteria = p_id_cafeteria;
END $$;

CREATE OR REPLACE PROCEDURE sp_favoritos_productos(IN p_id_usuario UUID, OUT p_lista JSONB)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  SELECT COALESCE(jsonb_agg(fn_favorito_producto_json(f.id_usuario, f.id_producto) ORDER BY f.creado_en DESC), '[]'::jsonb)
    INTO p_lista
    FROM favorito_producto f
   WHERE f.id_usuario = p_id_usuario;
END $$;

CREATE OR REPLACE PROCEDURE sp_guardar_favorito_producto(IN p_id_usuario UUID, IN p_id_producto UUID, OUT p_favorito JSONB)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  INSERT INTO favorito_producto (id_usuario, id_producto, id_cafeteria, nombre_guardado, precio_guardado, imagen_guardada)
  SELECT p_id_usuario, p.id_producto, p.id_cafeteria, p.nombre, p.precio, p.imagen_url
    FROM producto p JOIN cafeteria c ON c.id_cafeteria = p.id_cafeteria
   WHERE p.id_producto = p_id_producto AND p.disponible AND c.activa
  ON CONFLICT (id_usuario, id_producto) DO UPDATE
     SET nombre_guardado = EXCLUDED.nombre_guardado,
         precio_guardado = EXCLUDED.precio_guardado,
         imagen_guardada = EXCLUDED.imagen_guardada;
  IF NOT FOUND THEN
    RAISE EXCEPTION 'El platillo no existe o no está disponible' USING ERRCODE = 'no_data_found';
  END IF;
  p_favorito := fn_favorito_producto_json(p_id_usuario, p_id_producto);
END $$;

CREATE OR REPLACE PROCEDURE sp_quitar_favorito_producto(IN p_id_usuario UUID, IN p_id_producto UUID)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  DELETE FROM favorito_producto WHERE id_usuario = p_id_usuario AND id_producto = p_id_producto;
END $$;
