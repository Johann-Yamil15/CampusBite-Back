-- JJ-Sprint5 13/11/2026: promociones. Por ahora informativas (no cambian el precio de la orden).
-- id_cafeteria NULL = promoción de la plataforma (usa categoria para la franja del alumno).
CREATE TABLE IF NOT EXISTS promocion (
  id_promocion   UUID           PRIMARY KEY DEFAULT gen_random_uuid(),
  id_cafeteria   UUID           REFERENCES cafeteria(id_cafeteria) ON DELETE CASCADE,
  nombre         VARCHAR(60)    NOT NULL CHECK (length(btrim(nombre)) >= 3),
  descripcion    VARCHAR(140)   NOT NULL DEFAULT '',
  tipo           tipo_promocion NOT NULL,
  valor          NUMERIC(10,2)  NOT NULL CHECK (valor > 0 AND valor <= 2000),
  desde          DATE           NOT NULL,
  hasta          DATE           NOT NULL,
  activa         BOOLEAN        NOT NULL DEFAULT true,
  categoria      VARCHAR(50),
  creado_en      TIMESTAMPTZ    NOT NULL DEFAULT now(),
  actualizado_en TIMESTAMPTZ    NOT NULL DEFAULT now(),
  CHECK (hasta >= desde),
  CHECK (tipo <> 'porcentaje' OR (valor BETWEEN 1 AND 90 AND valor = trunc(valor)))
);
CREATE INDEX IF NOT EXISTS ix_promocion_cafeteria ON promocion (id_cafeteria);

-- Su trigger de actualizado_en está en 04_triggers/001_actualizado_en.sql (la función se crea después de este módulo).
