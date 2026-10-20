-- JY-Sprint2 09/10/2026: productos del menú
CREATE TABLE IF NOT EXISTS producto (
  id_producto    UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
  id_cafeteria   UUID          NOT NULL REFERENCES cafeteria(id_cafeteria),
  id_categoria   UUID          REFERENCES categoria(id_categoria) ON DELETE SET NULL,
  nombre         VARCHAR(100)  NOT NULL CHECK (length(btrim(nombre)) >= 2),
  descripcion    VARCHAR(500),
  precio         NUMERIC(8,2)  NOT NULL CHECK (precio > 0),
  imagen_url     VARCHAR(255),
  disponible     BOOLEAN       NOT NULL DEFAULT true,
  creado_en      TIMESTAMPTZ   NOT NULL DEFAULT now(),
  actualizado_en TIMESTAMPTZ   NOT NULL DEFAULT now(),
  UNIQUE (id_cafeteria, nombre)
);
