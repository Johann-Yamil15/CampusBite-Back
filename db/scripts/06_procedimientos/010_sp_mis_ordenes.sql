-- JY-Sprint2 09/10/2026: historial del alumno, más recientes primero, paginado (máximo 100 por página).
--   CALL sp_mis_ordenes(<id_alumno>, 20, 0, NULL, NULL);
-- JJ-Sprint5 13/11/2026: cada orden es la forma completa de fn_orden_json (con líneas, notas, motivo y código) y
-- p_total trae el total de órdenes del alumno para la paginación.
DROP PROCEDURE IF EXISTS sp_mis_ordenes(UUID, INT, INT, JSONB);
CREATE OR REPLACE PROCEDURE sp_mis_ordenes(
  IN  p_id_usuario     UUID,
  IN  p_limite         INT,
  IN  p_desplazamiento INT,
  OUT p_ordenes        JSONB,
  OUT p_total          INT
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  SELECT count(*) INTO p_total FROM orden o WHERE o.id_usuario = p_id_usuario;

  SELECT COALESCE(jsonb_agg(fn_orden_json(m.id_orden, true) ORDER BY m.creado_en DESC), '[]'::jsonb)
    INTO p_ordenes
    FROM (SELECT o.id_orden, o.creado_en
            FROM orden o
           WHERE o.id_usuario = p_id_usuario
           ORDER BY o.creado_en DESC
           LIMIT LEAST(GREATEST(COALESCE(p_limite, 20), 1), 100)
          OFFSET GREATEST(COALESCE(p_desplazamiento, 0), 0)) m;
END $$;
