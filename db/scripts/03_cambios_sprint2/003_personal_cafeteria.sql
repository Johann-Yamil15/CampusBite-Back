-- JY-Sprint2 09/10/2026: corrige el círculo usuario -> orden -> cafeteria -> usuario.
-- Antes "quién administra una cafetería" se guardaba dos veces: usuario.id_rol = 2 y cafeteria.id_admin,
-- sin nada que las uniera. Podía quedar una cafetería administrada por un alumno, o un encargado sin
-- cafetería, y las validaciones de permisos tenían que revisar las dos cosas.
-- Ahora la relación vive en un solo lugar: usuario.id_cafeteria, y un CHECK de la misma fila garantiza
-- que solo un admin_cafeteria (rol 2) la tenga. cafeteria ya no apunta a usuario: la FK va en un solo
-- sentido. Además una cafetería puede tener varios encargados.
ALTER TABLE usuario ADD COLUMN IF NOT EXISTS id_cafeteria UUID REFERENCES cafeteria(id_cafeteria);

DO $$
DECLARE
  v_n INT;
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.columns
              WHERE table_schema = 'public' AND table_name = 'cafeteria' AND column_name = 'id_admin') THEN
    -- Con el modelo nuevo cada encargado pertenece a una sola cafetería.
    SELECT count(*) INTO v_n
      FROM (SELECT c.id_admin FROM cafeteria c
             WHERE c.id_admin IS NOT NULL GROUP BY c.id_admin HAVING count(*) > 1) d;
    IF v_n > 0 THEN
      RAISE EXCEPTION '% usuario(s) administran más de una cafetería. Deja una sola por usuario antes de migrar.', v_n;
    END IF;

    UPDATE usuario u
       SET id_cafeteria = c.id_cafeteria
      FROM cafeteria c
     WHERE c.id_admin = u.id_usuario AND u.id_rol = 2;

    -- El dato inconsistente que permitía el modelo anterior: no se migra, se avisa.
    SELECT count(*) INTO v_n
      FROM cafeteria c JOIN usuario u ON u.id_usuario = c.id_admin
     WHERE u.id_rol <> 2;
    IF v_n > 0 THEN
      RAISE WARNING '% cafetería(s) tenían como id_admin a un usuario que no es admin_cafeteria; quedan sin encargado.', v_n;
    END IF;

    ALTER TABLE cafeteria DROP COLUMN id_admin;
  END IF;

  IF NOT EXISTS (SELECT 1 FROM pg_constraint
                  WHERE conrelid = 'usuario'::regclass AND conname = 'ck_usuario_cafeteria_solo_encargado') THEN
    ALTER TABLE usuario ADD CONSTRAINT ck_usuario_cafeteria_solo_encargado
      CHECK (id_cafeteria IS NULL OR id_rol = 2);
  END IF;
END $$;

COMMENT ON COLUMN usuario.id_cafeteria IS
  'Cafetería donde trabaja el encargado (solo rol 2). NULL para alumnos y admin_sistema.';
