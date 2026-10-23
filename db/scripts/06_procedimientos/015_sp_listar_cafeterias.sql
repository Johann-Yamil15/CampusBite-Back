-- JY-Sprint3 20/10/2026: TEC-08. Lista de cafeterías por nombre. Las inactivas solo las ve admin_sistema.
--   CALL sp_listar_cafeterias(<id_actor>, false, NULL);
-- p_cafeterias: [{"id_cafeteria", "nombre", "ubicacion", "es_del_campus", "punto_entrega", "activa",
--                 "metodos_pago": [...], "creado_en", "actualizado_en"}]
CREATE OR REPLACE PROCEDURE sp_listar_cafeterias(
  IN  p_id_actor           UUID,
  IN  p_incluir_inactivas  BOOLEAN,
  OUT p_cafeterias         JSONB
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  IF COALESCE(p_incluir_inactivas, false) THEN
    CALL sp_exigir_admin_sistema(p_id_actor, 'ver cafeterías inactivas');
  END IF;

  SELECT COALESCE(jsonb_agg(to_jsonb(v) ORDER BY v.nombre), '[]'::jsonb)
    INTO p_cafeterias
    FROM (SELECT c.id_cafeteria, c.nombre, c.ubicacion, c.es_del_campus, c.punto_entrega, c.activa,
                 c.metodos_pago, c.creado_en, c.actualizado_en
            FROM vw_cafeteria c
           WHERE c.activa OR COALESCE(p_incluir_inactivas, false)) v;
END $$;
