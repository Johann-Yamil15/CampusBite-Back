-- JY-Sprint2 09/10/2026: suscripciones a notificaciones push del navegador
CREATE TABLE IF NOT EXISTS suscripcion_push (
  id_suscripcion UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
  id_usuario     UUID         NOT NULL REFERENCES usuario(id_usuario) ON DELETE CASCADE,
  endpoint       TEXT         NOT NULL UNIQUE,
  llave_p256dh   VARCHAR(255) NOT NULL,
  llave_auth     VARCHAR(255) NOT NULL,
  creada_en      TIMESTAMPTZ  NOT NULL DEFAULT now()
);
