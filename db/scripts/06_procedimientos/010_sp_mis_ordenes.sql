-- JY-Sprint2 09/10/2026: historial del alumno, más recientes primero, paginado (máximo 100 por página).
--   CALL sp_mis_ordenes(<id_alumno>, 20, 0, NULL);
-- p_ordenes: [{"id_orden", "cafeteria", "punto_entrega", "estado", "recogida_en", "codigo_recogida",
--              "total", "metodo", "estado_pago", "creado_en"}]
CREATE OR REPLACE PROCEDURE sp_mis_ordenes(
  IN  p_id_usuario     UUID,
  IN  p_limite         INT,
  IN  p_desplazamiento INT,
  OUT p_ordenes        JSONB
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  SELECT COALESCE(jsonb_agg(to_jsonb(m) ORDER BY m.creado_en DESC), '[]'::jsonb)
    INTO p_ordenes
    FROM (SELECT v.id_orden, v.cafeteria, v.punto_entrega, v.estado, v.recogida_en, v.codigo_recogida,
                 v.total, v.metodo, v.estado_pago, v.creado_en
            FROM vw_mis_ordenes v
           WHERE v.id_usuario = p_id_usuario
           ORDER BY v.creado_en DESC
           LIMIT LEAST(GREATEST(COALESCE(p_limite, 20), 1), 100)
          OFFSET GREATEST(COALESCE(p_desplazamiento, 0), 0)) m;
END $$;
