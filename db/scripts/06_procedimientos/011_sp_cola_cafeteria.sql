-- JY-Sprint2 09/10/2026: pedidos activos de una cafetería, por hora de recogida. Solo su encargado o admin_sistema.
--   CALL sp_cola_cafeteria(<id_cafeteria>, <id_encargado>, NULL);
-- p_cola: [{"id_orden", "estado", "recogida_en", "notas", "alumno", "productos": [...], "total",
--           "metodo", "estado_pago"}]
CREATE OR REPLACE PROCEDURE sp_cola_cafeteria(
  IN  p_id_cafeteria UUID,
  IN  p_id_actor     UUID,
  OUT p_cola         JSONB
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  CALL sp_exigir_gestor_cafeteria(p_id_cafeteria, p_id_actor, 'ver la cola de esta cafetería');

  SELECT COALESCE(jsonb_agg(to_jsonb(q) ORDER BY q.recogida_en), '[]'::jsonb)
    INTO p_cola
    FROM (SELECT v.id_orden, v.estado, v.recogida_en, v.notas, v.alumno, v.productos,
                 v.total, v.metodo, v.estado_pago
            FROM vw_cola_cafeteria v
           WHERE v.id_cafeteria = p_id_cafeteria) q;
END $$;
