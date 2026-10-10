-- JY-Sprint2 09/10/2026: usuarios (alumnos, personal de cafetería y administradores).
-- id_cafeteria se agrega en 03_cambios_sprint2/003_personal_cafeteria.sql.
CREATE TABLE IF NOT EXISTS usuario (
  id_usuario      UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
  id_rol          SMALLINT     NOT NULL REFERENCES rol(id_rol),
  nombre          VARCHAR(80)  NOT NULL CHECK (length(btrim(nombre)) >= 2),
  correo          CITEXT       NOT NULL UNIQUE
                  CHECK (length(correo) <= 120
                         AND correo ~* '^[^@[:space:]]+@[^@[:space:]]+\.[^@[:space:]]+$'),
  contrasena_hash VARCHAR(255) NOT NULL,
  matricula       VARCHAR(20)  UNIQUE,
  telefono        VARCHAR(15)  CHECK (telefono ~ '^[0-9+]{10,15}$'),
  activo          BOOLEAN      NOT NULL DEFAULT true,
  creado_en       TIMESTAMPTZ  NOT NULL DEFAULT now(),
  actualizado_en  TIMESTAMPTZ  NOT NULL DEFAULT now()
);
