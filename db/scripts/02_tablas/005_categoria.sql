-- JY-Sprint2 09/10/2026: categorías del menú de cada cafetería
CREATE TABLE IF NOT EXISTS categoria (
  id_categoria UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
  id_cafeteria UUID        NOT NULL REFERENCES cafeteria(id_cafeteria) ON DELETE CASCADE,
  nombre       VARCHAR(60) NOT NULL CHECK (length(btrim(nombre)) >= 2),
  UNIQUE (id_cafeteria, nombre)
);
