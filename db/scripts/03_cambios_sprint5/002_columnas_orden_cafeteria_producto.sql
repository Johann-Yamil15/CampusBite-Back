-- JJ-Sprint5 13/11/2026: columnas nuevas.
--  * orden.motivo_cancelacion: lo ve el alumno en el detalle ("Motivo: ...").
--  * orden.intentos_entrega / bloqueo_entrega_hasta: máximo 5 códigos de recogida erróneos por orden en 10 min.
--  * cafeteria.logo_url / portada_url / minutos_estimados: tarjetas del inicio del alumno (opcionales).
--  * producto.minutos_preparacion / dietas: filtros del buscador (opcionales).
ALTER TABLE orden     ADD COLUMN IF NOT EXISTS motivo_cancelacion    VARCHAR(200);
ALTER TABLE orden     ADD COLUMN IF NOT EXISTS intentos_entrega      SMALLINT NOT NULL DEFAULT 0;
ALTER TABLE orden     ADD COLUMN IF NOT EXISTS bloqueo_entrega_hasta TIMESTAMPTZ;
ALTER TABLE cafeteria ADD COLUMN IF NOT EXISTS logo_url              VARCHAR(500);
ALTER TABLE cafeteria ADD COLUMN IF NOT EXISTS portada_url           VARCHAR(500);
ALTER TABLE cafeteria ADD COLUMN IF NOT EXISTS minutos_estimados     SMALLINT CHECK (minutos_estimados BETWEEN 1 AND 240);
ALTER TABLE producto  ADD COLUMN IF NOT EXISTS minutos_preparacion   SMALLINT CHECK (minutos_preparacion BETWEEN 1 AND 240);
ALTER TABLE producto  ADD COLUMN IF NOT EXISTS dietas                dieta[] NOT NULL DEFAULT '{}';
