-- JY-Sprint2 09/10/2026: renglones de la orden. subtotal es columna generada: no puede desincronizarse.
CREATE TABLE IF NOT EXISTS detalle_orden (
  id_detalle      UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
  id_orden        UUID          NOT NULL REFERENCES orden(id_orden) ON DELETE CASCADE,
  id_producto     UUID          NOT NULL REFERENCES producto(id_producto),
  cantidad        SMALLINT      NOT NULL CHECK (cantidad BETWEEN 1 AND 20),
  precio_unitario NUMERIC(8,2)  NOT NULL CHECK (precio_unitario > 0),
  subtotal        NUMERIC(10,2) GENERATED ALWAYS AS (cantidad * precio_unitario) STORED,
  UNIQUE (id_orden, id_producto)
);
