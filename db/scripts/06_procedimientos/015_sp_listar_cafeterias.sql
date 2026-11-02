-- JY-Sprint3 20/10/2026: TEC-08. Lista de cafeterías por nombre. Las inactivas solo las ve admin_sistema.
-- JY-Sprint4 31/10/2026: paginada y con búsqueda, para aguantar miles de cafeterías sin mandar todas de golpe.
--   CALL sp_listar_cafeterias(<id_actor>, false, 'café' o NULL, 20, 0, NULL, NULL);
-- p_buscar: parte del nombre, sin distinguir mayúsculas (NULL o vacío = todas).
-- p_limite: 1 a 100 (20 si es NULL). p_desplazamiento: cuántas saltar (0 si es NULL).
-- p_total: cuántas cumplen el filtro (para que el cliente calcule las páginas).
-- p_cafeterias: la página pedida, [{"id_cafeteria", "nombre", "ubicacion", "es_del_campus", "punto_entrega",
--               "activa", "metodos_pago": [...], "creado_en", "actualizado_en"}]
-- La primera versión no paginaba (p_id_actor, p_incluir_inactivas, OUT p_cafeterias); se quita esa firma.
DROP PROCEDURE IF EXISTS sp_listar_cafeterias(UUID, BOOLEAN, JSONB);

CREATE OR REPLACE PROCEDURE sp_listar_cafeterias(
  IN  p_id_actor           UUID,
  IN  p_incluir_inactivas  BOOLEAN,
  IN  p_buscar             VARCHAR,
  IN  p_limite             INT,
  IN  p_desplazamiento     INT,
  OUT p_total              INT,
  OUT p_cafeterias         JSONB
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_inactivas BOOLEAN := COALESCE(p_incluir_inactivas, false);
  -- % y _ se buscan como texto, no como comodines de LIKE
  v_patron    TEXT    := '%' || replace(replace(replace(btrim(COALESCE(p_buscar, '')), '\', '\\'), '%', '\%'), '_', '\_') || '%';
BEGIN
  IF v_inactivas THEN
    CALL sp_exigir_admin_sistema(p_id_actor, 'ver cafeterías inactivas');
  END IF;

  SELECT count(*) INTO p_total
    FROM cafeteria c
   WHERE (c.activa OR v_inactivas)
     AND c.nombre ILIKE v_patron;

  -- Primero se elige la página sobre la tabla (el ORDER BY coincide con ux_cafeteria_nombre) y solo para esas
  -- filas se juntan los métodos de pago de vw_cafeteria
  SELECT COALESCE(jsonb_agg(to_jsonb(v) - 'orden' ORDER BY v.orden), '[]'::jsonb)
    INTO p_cafeterias
    FROM (SELECT vc.id_cafeteria, vc.nombre, vc.ubicacion, vc.es_del_campus, vc.punto_entrega, vc.activa,
                 vc.metodos_pago, vc.creado_en, vc.actualizado_en, pagina.orden
            FROM (SELECT c.id_cafeteria,
                         row_number() OVER (ORDER BY lower(btrim(c.nombre)), c.id_cafeteria) AS orden
                    FROM cafeteria c
                   WHERE (c.activa OR v_inactivas)
                     AND c.nombre ILIKE v_patron
                   ORDER BY lower(btrim(c.nombre)), c.id_cafeteria
                   LIMIT LEAST(GREATEST(COALESCE(p_limite, 20), 1), 100)
                  OFFSET GREATEST(COALESCE(p_desplazamiento, 0), 0)) pagina
            JOIN vw_cafeteria vc ON vc.id_cafeteria = pagina.id_cafeteria) v;
END $$;
