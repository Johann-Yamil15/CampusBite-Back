-- JJ-Sprint5 13/11/2026: favoritos del alumno.
-- favorito_producto NO tiene FK a producto a propósito: el DELETE de producto es físico y el alumno debe seguir
-- viendo su favorito como "Ya no disponible" con el último nombre, precio e imagen (copia *_guardado).
CREATE TABLE IF NOT EXISTS favorito_cafeteria (
  id_usuario   UUID        NOT NULL REFERENCES usuario(id_usuario) ON DELETE CASCADE,
  id_cafeteria UUID        NOT NULL REFERENCES cafeteria(id_cafeteria) ON DELETE CASCADE,
  creado_en    TIMESTAMPTZ NOT NULL DEFAULT now(),
  PRIMARY KEY (id_usuario, id_cafeteria)
);

CREATE TABLE IF NOT EXISTS favorito_producto (
  id_usuario      UUID         NOT NULL REFERENCES usuario(id_usuario) ON DELETE CASCADE,
  id_producto     UUID         NOT NULL,
  id_cafeteria    UUID         NOT NULL REFERENCES cafeteria(id_cafeteria) ON DELETE CASCADE,
  nombre_guardado VARCHAR(100) NOT NULL,
  precio_guardado NUMERIC(8,2) NOT NULL,
  imagen_guardada VARCHAR(500),
  creado_en       TIMESTAMPTZ  NOT NULL DEFAULT now(),
  PRIMARY KEY (id_usuario, id_producto)
);
