-- JY-Sprint4 05/11/2026: prueba de carga de sp_listar_productos_cafeteria con 1500 productos en UNA cafetería.
-- Todo corre en una transacción que se revierte al final: no deja datos.
--   psql "<conexion local>" -v ON_ERROR_STOP=1 -f db/tests/carga_productos.sql
\set ON_ERROR_STOP on
BEGIN;

-- 1500 productos en la cafetería de prueba c1: la mitad en "Desayunos" (d1), un tercio sin categoría, el resto en
-- "Bebidas" (d2); uno de cada diez no disponible
INSERT INTO producto (id_cafeteria, id_categoria, nombre, descripcion, precio, disponible)
SELECT '00000000-0000-0000-0000-0000000000c1',
       CASE WHEN n % 2 = 0 THEN '00000000-0000-0000-0000-0000000000d1'::uuid
            WHEN n % 3 = 0 THEN NULL
            ELSE '00000000-0000-0000-0000-0000000000d2'::uuid END,
       format('Carga %s', lpad(n::text, 4, '0')), 'Producto de prueba', 10 + n % 190, n % 10 <> 0
  FROM generate_series(1, 1500) n;
ANALYZE producto;

DO $$
DECLARE
  c_cafe    UUID := '00000000-0000-0000-0000-0000000000c1';
  c_admin   UUID := '00000000-0000-0000-0000-0000000000a2';     -- encargado de c1
  c_alumno  UUID := '00000000-0000-0000-0000-0000000000a1';
  c_d1      UUID := '00000000-0000-0000-0000-0000000000d1';
  v_total   INT;
  v_pagina  JSONB;
  v_todos   INT;
  v_ok      BOOLEAN;
  v_inicio  TIMESTAMPTZ;
  v_ms      NUMERIC;
BEGIN
  SELECT count(*) INTO v_todos FROM producto WHERE id_cafeteria = c_cafe;
  SET LOCAL ROLE app_backend;

  CALL sp_listar_productos_cafeteria(c_cafe, c_admin, NULL, NULL, NULL, NULL, v_total, v_pagina);
  ASSERT v_total = v_todos AND v_total >= 1500, format('El total debe ser %s, fue %s', v_todos, v_total);
  ASSERT jsonb_array_length(v_pagina) = 20, 'Página de 20 por defecto';
  ASSERT NOT (v_pagina->0 ? 'orden'), 'La columna interna de orden no sale en el JSON';
  ASSERT v_pagina->0 ? 'categoria' AND v_pagina->0 ? 'disponible', 'Mismo objeto que antes (con categoría y disponible)';

  -- Búsqueda y categoría
  CALL sp_listar_productos_cafeteria(c_cafe, c_admin, 'carga', NULL, 100, 0, v_total, v_pagina);
  ASSERT v_total = 1500, format('Con "carga" deben ser 1500, fueron %s', v_total);
  CALL sp_listar_productos_cafeteria(c_cafe, c_admin, 'carga', c_d1, 100, 0, v_total, v_pagina);
  ASSERT v_total = 750, format('En Desayunos deben ser 750, fueron %s', v_total);
  ASSERT (SELECT bool_and(e->>'categoria' = 'Desayunos') FROM jsonb_array_elements(v_pagina) e), 'Solo de esa categoría';
  CALL sp_listar_productos_cafeteria(c_cafe, c_admin, '%', NULL, 20, 0, v_total, v_pagina);
  ASSERT v_total = 0, 'El % no es comodín';

  -- Orden: por categoría (sin categoría al final) y nombre; la última página trae solo lo que queda
  CALL sp_listar_productos_cafeteria(c_cafe, c_admin, 'carga', NULL, 100, 1400, v_total, v_pagina);
  ASSERT jsonb_array_length(v_pagina) = 100, 'La última página trae los 100 que quedan';
  ASSERT (SELECT bool_and(e->>'categoria' IS NULL) FROM jsonb_array_elements(v_pagina) e), 'Los sin categoría van al final';
  CALL sp_listar_productos_cafeteria(c_cafe, c_admin, 'carga', NULL, 100, 9000, v_total, v_pagina);
  ASSERT v_pagina = '[]'::jsonb AND v_total = 1500, 'Fuera de rango: vacía y con el total';
  CALL sp_listar_productos_cafeteria(c_cafe, c_admin, NULL, NULL, 5000, -1, v_total, v_pagina);
  ASSERT jsonb_array_length(v_pagina) = 100, 'Máximo 100 por página';

  v_ok := false;
  BEGIN CALL sp_listar_productos_cafeteria(c_cafe, c_alumno, NULL, NULL, 20, 0, v_total, v_pagina);
  EXCEPTION WHEN insufficient_privilege THEN v_ok := true; END;
  ASSERT v_ok, 'Un alumno no ve el panel de productos';

  v_inicio := clock_timestamp();
  FOR i IN 0..49 LOOP
    CALL sp_listar_productos_cafeteria(c_cafe, c_admin, NULL, NULL, 20, i * 30, v_total, v_pagina);
  END LOOP;
  v_ms := extract(epoch FROM clock_timestamp() - v_inicio) * 1000 / 50;
  RESET ROLE;

  RAISE NOTICE 'OK carga productos: % en la cafetería, página de 20 en % ms promedio', v_total, round(v_ms, 2);
  ASSERT v_ms < 200, format('Cada página debería tardar menos de 200 ms, tardó %s', round(v_ms, 2));
END $$;

ROLLBACK;
