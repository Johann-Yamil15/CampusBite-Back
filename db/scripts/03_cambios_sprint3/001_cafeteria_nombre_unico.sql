-- JY-Sprint3 20/10/2026: TEC-08. Dos cafeterías con el mismo nombre confunden al alumno al elegir dónde pedir.
-- Único sin distinguir mayúsculas ni espacios de los extremos ("Cafetería Central" = " cafetería central ").
-- Si una base ya tiene nombres repetidos, no se crea el índice y se avisa: hay que renombrar y volver a ejecutar.
DO $$
DECLARE
  v_repetidos INT;
BEGIN
  IF to_regclass('public.ux_cafeteria_nombre') IS NOT NULL THEN
    RETURN;
  END IF;

  SELECT count(*) INTO v_repetidos
    FROM (SELECT 1 FROM cafeteria c GROUP BY lower(btrim(c.nombre)) HAVING count(*) > 1) d;
  IF v_repetidos > 0 THEN
    RAISE WARNING '% nombre(s) de cafetería repetidos: no se creó ux_cafeteria_nombre. Renómbralos y vuelve a ejecutar.', v_repetidos;
    RETURN;
  END IF;

  CREATE UNIQUE INDEX ux_cafeteria_nombre ON cafeteria (lower(btrim(nombre)));
END $$;
