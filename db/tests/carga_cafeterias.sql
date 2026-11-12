-- JY-Sprint4 31/10/2026: prueba de carga de sp_listar_cafeterias con 2000 cafeterías (1500 activas).
-- Todo corre en una transacción que se revierte al final: no deja datos.
--   psql "<conexion local>" -v ON_ERROR_STOP=1 -f db/tests/carga_cafeterias.sql
\set ON_ERROR_STOP on
BEGIN;

-- 2000 cafeterías: las múltiplos de 4 quedan inactivas (500), el resto activas (1500). Cada una con 2 métodos.
INSERT INTO cafeteria (nombre, ubicacion, es_del_campus, punto_entrega, activa)
SELECT format('Carga %s', lpad(n::text, 4, '0')), 'Edificio de prueba', n % 2 = 0, 'Ventanilla', n % 4 <> 0
  FROM generate_series(1, 2000) n;
INSERT INTO cafeteria_metodo_pago (id_cafeteria, metodo)
SELECT c.id_cafeteria, m.metodo::metodo_pago
  FROM cafeteria c CROSS JOIN (VALUES ('efectivo'), ('tarjeta')) m(metodo)
 WHERE c.nombre LIKE 'Carga %';
ANALYZE cafeteria;
ANALYZE cafeteria_metodo_pago;

DO $$
DECLARE
  c_alumno  UUID := '00000000-0000-0000-0000-0000000000a1';
  c_sistema UUID := '00000000-0000-0000-0000-0000000000a3';
  v_total   INT;
  v_pagina  JSONB;
  v_ok      BOOLEAN;
  v_inicio  TIMESTAMPTZ;
  v_ms      NUMERIC;
  v_activas INT;
BEGIN
  SELECT count(*) INTO v_activas FROM cafeteria WHERE activa;
  SET LOCAL ROLE app_backend;

  -- Primera página: 20 por defecto, total de TODAS las activas (las de carga + las de los datos de prueba)
  CALL sp_listar_cafeterias(c_alumno, false, NULL, NULL, NULL, v_total, v_pagina);
  ASSERT v_total = v_activas, format('El total debe ser %s, fue %s', v_activas, v_total);
  ASSERT v_total >= 1500, 'Debe haber al menos 1500 activas';
  ASSERT jsonb_array_length(v_pagina) = 20, 'La página por defecto es de 20';
  ASSERT jsonb_array_length(v_pagina->0->'metodos_pago') >= 1, 'Cada cafetería trae sus métodos de pago';
  ASSERT NOT (v_pagina->0 ? 'orden'), 'La columna interna de orden no debe salir en el JSON';

  -- Página 3 de 50 con búsqueda: ordenada por nombre y sin inactivas
  CALL sp_listar_cafeterias(c_alumno, false, 'carga', 50, 100, v_total, v_pagina);
  ASSERT v_total = 1500, format('Con la búsqueda "carga" deben ser 1500 activas, fueron %s', v_total);
  ASSERT jsonb_array_length(v_pagina) = 50, 'Página de 50';
  ASSERT (SELECT bool_and((e->>'activa')::boolean) FROM jsonb_array_elements(v_pagina) e), 'Sin inactivas';
  ASSERT (SELECT array_agg(e->>'nombre' ORDER BY ord) = array_agg(e->>'nombre' ORDER BY lower(e->>'nombre'))
            FROM jsonb_array_elements(v_pagina) WITH ORDINALITY x(e, ord)), 'Ordenada por nombre';

  -- La última página trae solo lo que queda; más allá del final viene vacía pero con el total
  CALL sp_listar_cafeterias(c_alumno, false, 'carga', 100, 1450, v_total, v_pagina);
  ASSERT jsonb_array_length(v_pagina) = 50, 'La última página trae las 50 que quedan';
  CALL sp_listar_cafeterias(c_alumno, false, 'carga', 100, 5000, v_total, v_pagina);
  ASSERT v_pagina = '[]'::jsonb AND v_total = 1500, 'Fuera de rango: lista vacía y el total igual';

  -- Límites: más de 100 se recorta a 100; negativos y NULL usan los valores por defecto
  CALL sp_listar_cafeterias(c_alumno, false, NULL, 5000, -10, v_total, v_pagina);
  ASSERT jsonb_array_length(v_pagina) = 100, 'Máximo 100 por página';

  -- % y _ se buscan como texto
  CALL sp_listar_cafeterias(c_alumno, false, '%', 20, 0, v_total, v_pagina);
  ASSERT v_total = 0, 'El % no es comodín';

  -- Inactivas: solo admin_sistema
  CALL sp_listar_cafeterias(c_sistema, true, 'carga', 20, 0, v_total, v_pagina);
  ASSERT v_total = 2000, format('Con inactivas deben ser 2000, fueron %s', v_total);
  v_ok := false;
  BEGIN CALL sp_listar_cafeterias(c_alumno, true, NULL, 20, 0, v_total, v_pagina);
  EXCEPTION WHEN insufficient_privilege THEN v_ok := true; END;
  ASSERT v_ok, 'Un alumno no puede pedir inactivas';

  -- Tiempo: 50 llamadas a páginas distintas
  v_inicio := clock_timestamp();
  FOR i IN 0..49 LOOP
    CALL sp_listar_cafeterias(c_alumno, false, NULL, 20, i * 30, v_total, v_pagina);
  END LOOP;
  v_ms := extract(epoch FROM clock_timestamp() - v_inicio) * 1000 / 50;
  RESET ROLE;

  RAISE NOTICE 'OK carga: % activas, página de 20 en % ms promedio', v_total, round(v_ms, 2);
  ASSERT v_ms < 200, format('Cada página debería tardar menos de 200 ms, tardó %s', round(v_ms, 2));
END $$;

ROLLBACK;
