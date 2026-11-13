-- JJ-Sprint5 13/11/2026: "Reportar un problema" con un pedido. Uno por orden (UNIQUE id_orden).
CREATE TABLE IF NOT EXISTS reporte_orden (
  id_reporte UUID           PRIMARY KEY DEFAULT gen_random_uuid(),
  id_orden   UUID           NOT NULL UNIQUE REFERENCES orden(id_orden) ON DELETE CASCADE,
  motivo     motivo_reporte NOT NULL,
  detalle    VARCHAR(300),
  folio      CHAR(8)        NOT NULL UNIQUE CHECK (folio ~ '^R-[0-9]{6}$'),
  estado     VARCHAR(20)    NOT NULL DEFAULT 'abierto' CHECK (estado IN ('abierto','en_revision','cerrado')),
  creado_en  TIMESTAMPTZ    NOT NULL DEFAULT now()
);
