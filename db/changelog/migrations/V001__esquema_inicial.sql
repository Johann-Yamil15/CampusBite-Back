-- =====================================================================
-- CampusBite · TEC-03 · Migración V001: esquema inicial
-- Extensiones, tipos ENUM, tablas, restricciones, índices y catálogo de roles.
-- Origen: campusbite_TEC-02.sql (PostgreSQL 14+). Se aplica en una sola
-- transacción por Liquibase (ver changelog-master.xml).
-- =====================================================================

-- 0. Extensiones y tipos -----------------------------------------------
CREATE EXTENSION IF NOT EXISTS citext;   -- correo sin distinguir mayúsculas

CREATE TYPE estado_orden AS ENUM
  ('pendiente','confirmada','en_preparacion','lista','entregada','cancelada');
CREATE TYPE metodo_pago AS ENUM ('tarjeta','transferencia','efectivo');
CREATE TYPE estado_pago AS ENUM
  ('pendiente','pagado','rechazado','reembolsado','cancelado');

-- 1. Tablas -------------------------------------------------------------
CREATE TABLE rol (
  id_rol  SMALLINT    PRIMARY KEY,
  nombre  VARCHAR(30) NOT NULL UNIQUE
);
INSERT INTO rol (id_rol, nombre) VALUES
  (1,'alumno'), (2,'admin_cafeteria'), (3,'admin_sistema');

CREATE TABLE usuario (
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

CREATE TABLE cafeteria (
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

CREATE TABLE horario_cafeteria (
  id_horario    UUID     PRIMARY KEY DEFAULT gen_random_uuid(),
  id_cafeteria  UUID     NOT NULL REFERENCES cafeteria(id_cafeteria) ON DELETE CASCADE,
  dia_semana    SMALLINT NOT NULL CHECK (dia_semana BETWEEN 1 AND 7),  -- ISO: 1 = lunes
  hora_apertura TIME     NOT NULL,
  hora_cierre   TIME     NOT NULL,
  CHECK (hora_cierre > hora_apertura),
  UNIQUE (id_cafeteria, dia_semana, hora_apertura)
);

CREATE TABLE categoria (
  id_categoria UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
  id_cafeteria UUID        NOT NULL REFERENCES cafeteria(id_cafeteria) ON DELETE CASCADE,
  nombre       VARCHAR(60) NOT NULL CHECK (length(btrim(nombre)) >= 2),
  UNIQUE (id_cafeteria, nombre)
);

CREATE TABLE producto (
  id_producto    UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
  id_cafeteria   UUID          NOT NULL REFERENCES cafeteria(id_cafeteria),
  id_categoria   UUID          REFERENCES categoria(id_categoria) ON DELETE SET NULL,
  nombre         VARCHAR(100)  NOT NULL CHECK (length(btrim(nombre)) >= 2),
  descripcion    VARCHAR(500),
  precio         NUMERIC(8,2)  NOT NULL CHECK (precio > 0),
  imagen_url     VARCHAR(255),
  disponible     BOOLEAN       NOT NULL DEFAULT true,
  creado_en      TIMESTAMPTZ   NOT NULL DEFAULT now(),
  actualizado_en TIMESTAMPTZ   NOT NULL DEFAULT now(),
  UNIQUE (id_cafeteria, nombre)
);

CREATE TABLE orden (
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

CREATE TABLE detalle_orden (
  id_detalle      UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
  id_orden        UUID          NOT NULL REFERENCES orden(id_orden) ON DELETE CASCADE,
  id_producto     UUID          NOT NULL REFERENCES producto(id_producto),
  cantidad        SMALLINT      NOT NULL CHECK (cantidad BETWEEN 1 AND 20),
  precio_unitario NUMERIC(8,2)  NOT NULL CHECK (precio_unitario > 0),
  subtotal        NUMERIC(10,2) GENERATED ALWAYS AS (cantidad * precio_unitario) STORED,
  UNIQUE (id_orden, id_producto)
);

CREATE TABLE pago (
  id_pago        UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
  id_orden       UUID          NOT NULL UNIQUE REFERENCES orden(id_orden) ON DELETE CASCADE,
  metodo         metodo_pago   NOT NULL,
  monto          NUMERIC(10,2) NOT NULL CHECK (monto > 0),
  estado         estado_pago   NOT NULL DEFAULT 'pendiente',
  referencia     VARCHAR(100),
  pagado_en      TIMESTAMPTZ,
  actualizado_en TIMESTAMPTZ   NOT NULL DEFAULT now(),
  CHECK ((estado IN ('pagado','reembolsado')) = (pagado_en IS NOT NULL))
);

CREATE TABLE historial_estado_orden (
  id_historial UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
  id_orden     UUID         NOT NULL REFERENCES orden(id_orden) ON DELETE CASCADE,
  estado       estado_orden NOT NULL,
  cambiado_por UUID         REFERENCES usuario(id_usuario),
  cambiado_en  TIMESTAMPTZ  NOT NULL DEFAULT now()
);

CREATE TABLE suscripcion_push (
  id_suscripcion UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
  id_usuario     UUID         NOT NULL REFERENCES usuario(id_usuario) ON DELETE CASCADE,
  endpoint       TEXT         NOT NULL UNIQUE,
  llave_p256dh   VARCHAR(255) NOT NULL,
  llave_auth     VARCHAR(255) NOT NULL,
  creada_en      TIMESTAMPTZ  NOT NULL DEFAULT now()
);

-- 2. Índices (PostgreSQL no indexa las FK automáticamente) -------------
CREATE INDEX idx_usuario_rol            ON usuario(id_rol);
CREATE INDEX idx_cafeteria_admin        ON cafeteria(id_admin);
CREATE INDEX idx_cafeteria_activa       ON cafeteria(es_del_campus) WHERE activa;
CREATE INDEX idx_horario_cafeteria_dia  ON horario_cafeteria(id_cafeteria, dia_semana);
CREATE INDEX idx_producto_cafeteria     ON producto(id_cafeteria, id_categoria) WHERE disponible;
CREATE INDEX idx_producto_categoria     ON producto(id_categoria);
CREATE INDEX idx_orden_usuario_fecha    ON orden(id_usuario, creada_en DESC);
CREATE INDEX idx_orden_cola_cafeteria   ON orden(id_cafeteria, hora_recogida)
                                        WHERE estado IN ('confirmada','en_preparacion','lista');
CREATE INDEX idx_detalle_producto       ON detalle_orden(id_producto);   -- (id_orden) ya lo cubre el UNIQUE
CREATE INDEX idx_historial_orden_fecha  ON historial_estado_orden(id_orden, cambiado_en);
CREATE INDEX idx_push_usuario           ON suscripcion_push(id_usuario);
