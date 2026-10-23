-- JY-Sprint2 09/10/2026: bitácora de estados de cada orden (la llena un trigger, ver 04_triggers)
CREATE TABLE IF NOT EXISTS historial_estado_orden (
  id_historial UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
  id_orden     UUID         NOT NULL REFERENCES orden(id_orden) ON DELETE CASCADE,
  estado       estado_orden NOT NULL,
  cambiado_por UUID         REFERENCES usuario(id_usuario),
  cambiado_en  TIMESTAMPTZ  NOT NULL DEFAULT now()
);
