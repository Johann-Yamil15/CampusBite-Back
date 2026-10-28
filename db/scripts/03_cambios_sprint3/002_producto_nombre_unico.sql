-- JY-Sprint4 27/10/2026: TEC-09/TEC-10. Dos productos con el mismo nombre en un menú confunden al alumno.
-- Único por cafetería sin distinguir mayúsculas ni espacios de los extremos ("Café Americano" = " café americano ").
-- También evita el duplicado cuando el celular repite el alta por mala señal: la segunda responde 23505.
-- Si una base ya tiene nombres repetidos, no se crea el índice y se avisa: hay que renombrar y volver a ejecutar.
DO $$
DECLARE
  v_repetidos INT;
BEGIN
  IF to_regclass('public.ux_producto_nombre') IS NOT NULL THEN
    RETURN;
  END IF;

  SELECT count(*) INTO v_repetidos
    FROM (SELECT 1 FROM producto p GROUP BY p.id_cafeteria, lower(btrim(p.nombre)) HAVING count(*) > 1) d;
  IF v_repetidos > 0 THEN
    RAISE WARNING '% nombre(s) de producto repetidos en una misma cafetería: no se creó ux_producto_nombre. Renómbralos y vuelve a ejecutar.', v_repetidos;
    RETURN;
  END IF;

  CREATE UNIQUE INDEX ux_producto_nombre ON producto (id_cafeteria, lower(btrim(nombre)));
END $$;
