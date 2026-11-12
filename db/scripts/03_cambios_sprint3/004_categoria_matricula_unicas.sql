-- JY-Sprint4 05/11/2026: evita duplicados que hoy pasan por diferencias de mayúsculas o espacios.
-- Categoría: única por cafetería sin distinguir mayúsculas ("Bebidas" = " bebidas "); la restricción original
--   UNIQUE (id_cafeteria, nombre) solo detectaba nombres idénticos.
-- Matrícula: única sin distinguir mayúsculas ("UTT001" = "utt001"); la columna ya era UNIQUE pero exacta.
-- Igual que 001/002: si una base ya tiene repetidos no se crea el índice y se avisa (hay que corregirlos y volver a
-- ejecutar). El smoke test revisa que los índices existan, para que un aviso no pase desapercibido.
DO $$
DECLARE
  v_repetidos INT;
BEGIN
  IF to_regclass('public.ux_categoria_nombre') IS NULL THEN
    SELECT count(*) INTO v_repetidos
      FROM (SELECT 1 FROM categoria c GROUP BY c.id_cafeteria, lower(btrim(c.nombre)) HAVING count(*) > 1) d;
    IF v_repetidos > 0 THEN
      RAISE WARNING '% nombre(s) de categoría repetidos en una misma cafetería: no se creó ux_categoria_nombre. Renómbralos y vuelve a ejecutar.', v_repetidos;
    ELSE
      CREATE UNIQUE INDEX ux_categoria_nombre ON categoria (id_cafeteria, lower(btrim(nombre)));
    END IF;
  END IF;

  IF to_regclass('public.ux_usuario_matricula') IS NULL THEN
    SELECT count(*) INTO v_repetidos
      FROM (SELECT 1 FROM usuario u WHERE u.matricula IS NOT NULL
             GROUP BY upper(btrim(u.matricula)) HAVING count(*) > 1) d;
    IF v_repetidos > 0 THEN
      RAISE WARNING '% matrícula(s) repetidas (sin distinguir mayúsculas): no se creó ux_usuario_matricula. Corrígelas y vuelve a ejecutar.', v_repetidos;
    ELSE
      CREATE UNIQUE INDEX ux_usuario_matricula ON usuario (upper(btrim(matricula))) WHERE matricula IS NOT NULL;
    END IF;
  END IF;
END $$;
