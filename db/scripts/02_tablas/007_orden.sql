-- JY-Sprint2 09/10/2026: órdenes. total, hora_recogida, creada_en y actualizado_en se ajustan en
-- 03_cambios_sprint2 (004_quitar_total_orden.sql y 005_fechas.sql).
CREATE TABLE IF NOT EXISTS orden (
  id_orden        UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
  id_usuario      UUID          NOT NULL REFERENCES usuario(id_usuario),
  id_cafeteria    UUID          NOT NULL REFERENCES cafeteria(id_cafeteria),
  estado          estado_orden  NOT NULL DEFAULT 'pendiente',
  hora_recogida   TIMESTAMPTZ   NOT NULL,
  codigo_recogida CHAR(6)       NOT NULL CHECK (codigo_recogida ~ '^[0-9]{6}$'),
  total           NUMERIC(10,2) NOT NULL CHECK (total > 0),
  notas           VARCHAR(200),
  creada_en       TIMESTAMPTZ   NOT NULL DEFAULT now(),
  actualizado_en  TIMESTAMPTZ   NOT NULL DEFAULT now()
);
