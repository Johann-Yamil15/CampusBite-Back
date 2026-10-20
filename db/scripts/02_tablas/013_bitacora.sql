-- JY-Sprint2 10/10/2026: bitácora general. Una fila por cada INSERT, UPDATE o DELETE en cualquier tabla del
-- sistema: qué tabla, qué registro, qué operación, cómo estaba antes, cómo quedó, quién y cuándo.
-- La llenan los triggers de 04_triggers/008_bitacora.sql. Nadie la edita: solo se agregan filas.
-- Nunca guarda contrasena_hash.
CREATE TABLE IF NOT EXISTS bitacora (
  id_bitacora   BIGINT       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  tabla         VARCHAR(63)  NOT NULL,
  id_registro   TEXT         NOT NULL,          -- llave del registro; si es compuesta, valores unidos con ':'
  operacion     CHAR(1)      NOT NULL CHECK (operacion IN ('I','U','D')),   -- I = alta, U = cambio, D = baja
  datos_antes   JSONB,                          -- NULL en altas
  datos_despues JSONB,                          -- NULL en bajas
  cambiado_por  UUID,                           -- usuario que hizo el cambio (app.id_actor); NULL = sistema
  cambiado_en   TIMESTAMPTZ  NOT NULL DEFAULT now(),
  CHECK ((operacion = 'I') = (datos_antes IS NULL)),
  CHECK ((operacion = 'D') = (datos_despues IS NULL))
);

-- Consulta típica: "historial de este registro", del más reciente al más antiguo
DO $$
BEGIN
  IF to_regclass('public.idx_bitacora_registro') IS NULL THEN
    CREATE INDEX idx_bitacora_registro ON bitacora (tabla, id_registro, cambiado_en DESC);
  END IF;
END $$;
