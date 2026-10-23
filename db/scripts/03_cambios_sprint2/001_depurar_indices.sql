-- JY-Sprint2 09/10/2026: índices que V005 quitó por redundantes o sin uso (ver db/README.md).
-- En una base nueva ya no existen; en una de V1.0.0.001 se borran.
DROP INDEX IF EXISTS idx_horario_cafeteria_dia;   -- lo cubre el UNIQUE (id_cafeteria, dia_semana, hora_apertura)
DROP INDEX IF EXISTS idx_usuario_rol;             -- 3 valores posibles: nunca es selectivo
DROP INDEX IF EXISTS idx_cafeteria_activa;        -- tabla de decenas de filas
DROP INDEX IF EXISTS idx_cafeteria_admin;         -- la columna id_admin desaparece en 003_personal_cafeteria.sql
DROP INDEX IF EXISTS idx_producto_categoria;      -- tabla pequeña; las categorías casi nunca se borran
