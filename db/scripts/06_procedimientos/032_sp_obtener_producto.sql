-- JY-Sprint4 27/10/2026: TEC-09. Un producto de una cafetería por id.
--   CALL sp_obtener_producto(<id_cafeteria>, <id_producto>, <id_actor>, NULL);
-- p_producto: el producto como JSON (con el nombre de su categoría), o NULL si no existe o no es de esa cafetería.
-- Un producto no disponible, o de una cafetería inactiva, solo lo ven su encargado y admin_sistema; para los demás
-- también es NULL (igual que sp_obtener_cafeteria: no se revela que existe). Lo usa también el alta (sp_crear_producto
-- solo guarda) para responder con el producto creado.
CREATE OR REPLACE PROCEDURE sp_obtener_producto(
  IN  p_id_cafeteria UUID,
  IN  p_id_producto  UUID,
  IN  p_id_actor     UUID,
  OUT p_producto     JSONB
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  SELECT to_jsonb(v)
    INTO p_producto
    FROM (SELECT p.id_producto, p.id_cafeteria, p.id_categoria, cat.nombre AS categoria, p.nombre, p.descripcion,
                 p.precio, p.imagen_url, p.disponible, p.creado_en, p.actualizado_en
            FROM producto p
            JOIN cafeteria c        ON c.id_cafeteria = p.id_cafeteria
            LEFT JOIN categoria cat ON cat.id_categoria = p.id_categoria
           WHERE p.id_producto = p_id_producto
             AND p.id_cafeteria = p_id_cafeteria
             AND ((c.activa AND p.disponible) OR EXISTS (
                   SELECT 1 FROM usuario u
                    WHERE u.id_usuario = p_id_actor AND u.activo
                      AND (u.id_rol = 3 OR (u.id_rol = 2 AND u.id_cafeteria = c.id_cafeteria))))) v;
END $$;
