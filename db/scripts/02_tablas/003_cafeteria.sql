-- JY-Sprint2 09/10/2026: cafeterías. id_admin y acepta_* se reemplazan en 03_cambios_sprint2
-- (002_metodos_pago_cafeteria.sql y 003_personal_cafeteria.sql).
CREATE TABLE IF NOT EXISTS cafeteria (
  id_cafeteria         UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
  id_admin             UUID         REFERENCES usuario(id_usuario),
  nombre               VARCHAR(100) NOT NULL CHECK (length(btrim(nombre)) >= 2),
  ubicacion            VARCHAR(150) NOT NULL,
  es_del_campus        BOOLEAN      NOT NULL,
  punto_entrega        VARCHAR(100) NOT NULL,
  acepta_efectivo      BOOLEAN      NOT NULL DEFAULT true,
  acepta_tarjeta       BOOLEAN      NOT NULL DEFAULT true,
  acepta_transferencia BOOLEAN      NOT NULL DEFAULT true,
  activa               BOOLEAN      NOT NULL DEFAULT true,
  creado_en            TIMESTAMPTZ  NOT NULL DEFAULT now(),
  actualizado_en       TIMESTAMPTZ  NOT NULL DEFAULT now(),
  CHECK (acepta_efectivo OR acepta_tarjeta OR acepta_transferencia)
);
