-- JY-Sprint3 20/10/2026: TEC-08. Una cafetería por id.
--   CALL sp_obtener_cafeteria(<id_cafeteria>, <id_actor>, NULL);
-- p_cafeteria: el mismo objeto que sp_listar_cafeterias, o NULL si no existe. Una inactiva solo la ven su
-- encargado y admin_sistema; para los demás también es NULL (no se revela que existe).
CREATE OR REPLACE PROCEDURE sp_obtener_cafeteria(
  IN  p_id_cafeteria UUID,
  IN  p_id_actor     UUID,
  OUT p_cafeteria    JSONB
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  SELECT to_jsonb(v)
    INTO p_cafeteria
    FROM (SELECT c.id_cafeteria, c.nombre, c.ubicacion, c.es_del_campus, c.punto_entrega, c.activa,
                 c.metodos_pago, c.creado_en, c.actualizado_en
            FROM vw_cafeteria c
           WHERE c.id_cafeteria = p_id_cafeteria
             AND (c.activa OR EXISTS (
                   SELECT 1 FROM usuario u
                    WHERE u.id_usuario = p_id_actor AND u.activo
                      AND (u.id_rol = 3 OR (u.id_rol = 2 AND u.id_cafeteria = c.id_cafeteria))))) v;
END $$;
