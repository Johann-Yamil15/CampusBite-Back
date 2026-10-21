-- JY-Sprint3 20/10/2026: TEC-08. Lista de cafeterías por nombre, PAGINADA y con búsqueda. Las inactivas solo
-- las ve admin_sistema.
--   CALL sp_listar_cafeterias(<id_actor>, false, 'norte', 20, 0, NULL, NULL);
-- p_buscar: texto dentro del nombre, sin distinguir mayúsculas (NULL o '' = todas).
-- p_limite: 1 a 100 (por defecto 20). p_desplazamiento: cuántas saltar.
-- p_total: cuántas cumplen el filtro (para que el front calcule las páginas).
-- p_cafeterias: [{"id_cafeteria", "nombre", "ubicacion", "es_del_campus", "punto_entrega", "activa",
--                 "metodos_pago": [...], "creado_en", "actualizado_en"}]
-- Las pruebas de estrés mostraron que sin paginar, con 10 mil cafeterías, respondía 3 MB en ~150 ms.

-- La versión anterior (sin paginar) tenía otros parámetros: CREATE OR REPLACE no puede cambiarlos.
DO $$
DECLARE
  v_firma REGPROCEDURE;
BEGIN
  FOR v_firma IN
    SELECT p.oid::regprocedure FROM pg_proc p
     WHERE p.pronamespace = 'public'::regnamespace AND p.proname = 'sp_listar_cafeterias' AND p.pronargs <> 7
  LOOP
    EXECUTE format('DROP PROCEDURE %s', v_firma);
  END LOOP;
END $$;

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
  v_buscar    TEXT    := NULLIF(lower(btrim(COALESCE(p_buscar, ''))), '');
  v_ids       UUID[];
BEGIN
  IF v_inactivas THEN
    CALL sp_exigir_admin_sistema(p_id_actor, 'ver cafeterías inactivas');
  END IF;

  SELECT count(*) INTO p_total
    FROM cafeteria c
   WHERE (c.activa OR v_inactivas)
     AND (v_buscar IS NULL OR strpos(lower(c.nombre), v_buscar) > 0);   -- strpos: sin comodines que escapar

  -- Primero los ids de la página (sobre la tabla, con el orden del índice ux_cafeteria_nombre) y luego solo
  -- esos se arman con sus métodos de pago
  SELECT array_agg(p.id_cafeteria ORDER BY p.orden)
    INTO v_ids
    FROM (SELECT c.id_cafeteria, row_number() OVER (ORDER BY lower(btrim(c.nombre)), c.id_cafeteria) AS orden
            FROM cafeteria c
           WHERE (c.activa OR v_inactivas)
             AND (v_buscar IS NULL OR strpos(lower(c.nombre), v_buscar) > 0)
           ORDER BY lower(btrim(c.nombre)), c.id_cafeteria
           LIMIT LEAST(GREATEST(COALESCE(p_limite, 20), 1), 100)
          OFFSET GREATEST(COALESCE(p_desplazamiento, 0), 0)) p;

  SELECT COALESCE(jsonb_agg(to_jsonb(v) ORDER BY array_position(v_ids, v.id_cafeteria)), '[]'::jsonb)
    INTO p_cafeterias
    FROM (SELECT c.id_cafeteria, c.nombre, c.ubicacion, c.es_del_campus, c.punto_entrega, c.activa,
                 c.metodos_pago, c.creado_en, c.actualizado_en
            FROM vw_cafeteria c
           WHERE c.id_cafeteria = ANY (COALESCE(v_ids, '{}'))) v;
END $$;
