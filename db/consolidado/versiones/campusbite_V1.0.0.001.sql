-- =====================================================================
-- CampusBite · Script consolidado de base de datos
-- Versión : V1.0.0.001
-- Fecha   : 2026-10-09 16:12:10 -0600
-- Autor   : Johann Yamil
-- Huella  : 3b3a2842e3aa
-- Incluye : 7 scripts (ver orden.txt)
--   - changelog/migrations/V001__esquema_inicial.sql
--   - changelog/migrations/V002__logica_negocio.sql
--   - changelog/migrations/V003__vistas_y_seguridad.sql
--   - changelog/migrations/V004__endurecimiento_seguridad.sql
--   - changelog/migrations/V005__depuracion_indices.sql
--   - consolidado/dev/01_login_app_backend_dev.sql
--   - changelog/seeds/seed_dev.sql
--
-- SOLO PARA DESARROLLO LOCAL, sobre una base VACÍA:
--   createdb -U postgres campusbite
--   psql -U postgres -d campusbite -f campusbite_V1.0.0.001.sql
-- Corre todo en una transacción: si algo falla, no queda nada a medias.
-- Si la base ya tiene el esquema, se detiene sin cambiar nada (protege bases existentes).
-- Producción NO usa este archivo: se migra con Liquibase (db/changelog).
-- =====================================================================
\set ON_ERROR_STOP on
SET client_encoding = 'UTF8';

BEGIN;

-- Protección: abortar si la base ya tiene el esquema de CampusBite
DO $$
BEGIN
  IF to_regclass('public.usuario') IS NOT NULL THEN
    RAISE EXCEPTION 'La base % ya tiene el esquema de CampusBite. Este script es solo para una base vacía (local).', current_database();
  END IF;
END $$;

-- ---------------------------------------------------------------------
-- ---> changelog/migrations/V001__esquema_inicial.sql
-- ---------------------------------------------------------------------
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


-- ---------------------------------------------------------------------
-- ---> changelog/migrations/V002__logica_negocio.sql
-- ---------------------------------------------------------------------
-- =====================================================================
-- CampusBite · TEC-03 · Migración V002: lógica de negocio
-- Triggers (auditoría, máquina de estados, historial, pagos) y procedimientos almacenados.
-- Origen: campusbite_TEC-02.sql (PostgreSQL 14+). Se aplica en una sola
-- transacción por Liquibase (ver changelog-master.xml).
-- =====================================================================

-- 3. Triggers ------------------------------------------------------------
-- 3.1 Mantiene actualizado_en
CREATE FUNCTION fn_set_actualizado_en() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
  NEW.actualizado_en := now();
  RETURN NEW;
END $$;

CREATE TRIGGER trg_usuario_upd   BEFORE UPDATE ON usuario   FOR EACH ROW EXECUTE FUNCTION fn_set_actualizado_en();
CREATE TRIGGER trg_cafeteria_upd BEFORE UPDATE ON cafeteria FOR EACH ROW EXECUTE FUNCTION fn_set_actualizado_en();
CREATE TRIGGER trg_producto_upd  BEFORE UPDATE ON producto  FOR EACH ROW EXECUTE FUNCTION fn_set_actualizado_en();
CREATE TRIGGER trg_orden_upd     BEFORE UPDATE ON orden     FOR EACH ROW EXECUTE FUNCTION fn_set_actualizado_en();
CREATE TRIGGER trg_pago_upd      BEFORE UPDATE ON pago      FOR EACH ROW EXECUTE FUNCTION fn_set_actualizado_en();

-- 3.2 Máquina de estados de la orden
CREATE FUNCTION fn_validar_transicion_orden() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
  IF NEW.estado = OLD.estado THEN RETURN NEW; END IF;
  IF NOT (
       (OLD.estado = 'pendiente'      AND NEW.estado IN ('confirmada','cancelada'))
    OR (OLD.estado = 'confirmada'     AND NEW.estado IN ('en_preparacion','cancelada'))
    OR (OLD.estado = 'en_preparacion' AND NEW.estado IN ('lista','cancelada'))
    OR (OLD.estado = 'lista'          AND NEW.estado IN ('entregada','cancelada'))
  ) THEN
    RAISE EXCEPTION 'Transición de estado no permitida: % -> %', OLD.estado, NEW.estado
      USING ERRCODE = 'check_violation';
  END IF;
  RETURN NEW;
END $$;

CREATE TRIGGER trg_orden_transicion BEFORE UPDATE OF estado ON orden
  FOR EACH ROW EXECUTE FUNCTION fn_validar_transicion_orden();

-- 3.3 Bitácora automática de estados (el actor llega vía app.id_actor)
CREATE FUNCTION fn_historial_orden() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
  INSERT INTO historial_estado_orden (id_orden, estado, cambiado_por)
  VALUES (NEW.id_orden, NEW.estado,
          NULLIF(current_setting('app.id_actor', true), '')::uuid);
  RETURN NEW;
END $$;

CREATE TRIGGER trg_orden_historial_ins AFTER INSERT ON orden
  FOR EACH ROW EXECUTE FUNCTION fn_historial_orden();
CREATE TRIGGER trg_orden_historial_upd AFTER UPDATE OF estado ON orden
  FOR EACH ROW WHEN (OLD.estado IS DISTINCT FROM NEW.estado)
  EXECUTE FUNCTION fn_historial_orden();

-- 3.4 Al cancelar una orden se cierra su pago
CREATE FUNCTION fn_cancelar_pago() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
  UPDATE pago
     SET estado    = CASE WHEN estado = 'pagado' THEN 'reembolsado'::estado_pago
                          ELSE 'cancelado'::estado_pago END,
         pagado_en = CASE WHEN estado = 'pagado' THEN pagado_en ELSE NULL END
   WHERE id_orden = NEW.id_orden AND estado IN ('pendiente','pagado');
  RETURN NEW;
END $$;

CREATE TRIGGER trg_orden_cancelada AFTER UPDATE OF estado ON orden
  FOR EACH ROW WHEN (NEW.estado = 'cancelada' AND OLD.estado <> 'cancelada')
  EXECUTE FUNCTION fn_cancelar_pago();

-- 3.5 Un detalle solo puede llevar productos de la cafetería de su orden
CREATE FUNCTION fn_validar_detalle() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
  IF (SELECT p.id_cafeteria FROM producto p WHERE p.id_producto = NEW.id_producto)
     IS DISTINCT FROM
     (SELECT o.id_cafeteria FROM orden o WHERE o.id_orden = NEW.id_orden) THEN
    RAISE EXCEPTION 'El producto no pertenece a la cafetería de la orden'
      USING ERRCODE = 'check_violation';
  END IF;
  RETURN NEW;
END $$;

CREATE TRIGGER trg_detalle_valida BEFORE INSERT OR UPDATE ON detalle_orden
  FOR EACH ROW EXECUTE FUNCTION fn_validar_detalle();

-- 4. Procedimientos almacenados -----------------------------------------
-- 4.1 Registro de alumno
CREATE FUNCTION sp_registrar_usuario(
  p_nombre VARCHAR, p_correo VARCHAR, p_contrasena_hash VARCHAR,
  p_matricula VARCHAR DEFAULT NULL, p_telefono VARCHAR DEFAULT NULL
) RETURNS UUID
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE v_id UUID;
BEGIN
  INSERT INTO usuario (id_rol, nombre, correo, contrasena_hash, matricula, telefono)
  VALUES (1, btrim(p_nombre), p_correo, p_contrasena_hash, p_matricula, p_telefono)
  RETURNING id_usuario INTO v_id;
  RETURN v_id;
EXCEPTION WHEN unique_violation THEN
  RAISE EXCEPTION 'El correo o la matrícula ya están registrados' USING ERRCODE = 'unique_violation';
END $$;

-- 4.2 Credenciales para el login (el hash se compara en el backend)
CREATE FUNCTION sp_obtener_credenciales(p_correo VARCHAR)
RETURNS TABLE (id_usuario UUID, id_rol SMALLINT, contrasena_hash VARCHAR, activo BOOLEAN)
LANGUAGE sql STABLE SECURITY DEFINER SET search_path = public, pg_temp AS $$
  SELECT u.id_usuario, u.id_rol, u.contrasena_hash, u.activo
    FROM usuario u WHERE u.correo = p_correo::citext;
$$;

-- 4.3 Crear orden completa (orden + detalles + pago) en una transacción
--     p_items: [{"id_producto":"<uuid>","cantidad":2}, ...]
CREATE FUNCTION sp_crear_orden(
  p_id_usuario UUID, p_id_cafeteria UUID, p_hora_recogida TIMESTAMPTZ,
  p_metodo metodo_pago, p_items JSONB, p_notas VARCHAR DEFAULT NULL
) RETURNS UUID
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_tz       CONSTANT TEXT := 'America/Mexico_City';
  v_local    TIMESTAMP;
  v_n_items  INT; v_n_validos INT; v_cant_ok BOOLEAN;
  v_total    NUMERIC(10,2);
  v_id_orden UUID;
  v_cafe     cafeteria%ROWTYPE;
  v_acepta   BOOLEAN;
BEGIN
  IF NOT EXISTS (SELECT 1 FROM usuario WHERE id_usuario = p_id_usuario AND id_rol = 1 AND activo) THEN
    RAISE EXCEPTION 'Usuario no válido para realizar órdenes';
  END IF;

  SELECT * INTO v_cafe FROM cafeteria WHERE id_cafeteria = p_id_cafeteria AND activa;
  IF NOT FOUND THEN RAISE EXCEPTION 'La cafetería no existe o no está activa'; END IF;

  v_acepta := CASE p_metodo WHEN 'efectivo' THEN v_cafe.acepta_efectivo
                            WHEN 'tarjeta'  THEN v_cafe.acepta_tarjeta
                            ELSE v_cafe.acepta_transferencia END;
  IF NOT v_acepta THEN
    RAISE EXCEPTION 'La cafetería no acepta el método de pago %', p_metodo;
  END IF;

  IF p_hora_recogida < now() + interval '5 minutes' THEN
    RAISE EXCEPTION 'La hora de recogida debe ser al menos 5 minutos en el futuro';
  END IF;
  v_local := p_hora_recogida AT TIME ZONE v_tz;
  IF NOT EXISTS (
    SELECT 1 FROM horario_cafeteria h
     WHERE h.id_cafeteria = p_id_cafeteria
       AND h.dia_semana = EXTRACT(ISODOW FROM v_local)
       AND v_local::time BETWEEN h.hora_apertura AND h.hora_cierre
  ) THEN
    RAISE EXCEPTION 'La cafetería está cerrada a la hora de recogida solicitada';
  END IF;

  IF jsonb_typeof(p_items) <> 'array' OR jsonb_array_length(p_items) = 0 THEN
    RAISE EXCEPTION 'La orden debe incluir al menos un producto';
  END IF;

  WITH items AS (
    SELECT (e->>'id_producto')::uuid AS id_producto, SUM((e->>'cantidad')::int) AS cantidad
      FROM jsonb_array_elements(p_items) e GROUP BY 1
  )
  SELECT count(*), count(p.id_producto), COALESCE(bool_and(i.cantidad BETWEEN 1 AND 20), false),
         SUM(i.cantidad * p.precio)
    INTO v_n_items, v_n_validos, v_cant_ok, v_total
    FROM items i
    LEFT JOIN producto p ON p.id_producto = i.id_producto
                        AND p.id_cafeteria = p_id_cafeteria AND p.disponible;

  IF v_n_items <> v_n_validos THEN
    RAISE EXCEPTION 'Hay productos inexistentes, no disponibles o de otra cafetería';
  END IF;
  IF NOT v_cant_ok THEN RAISE EXCEPTION 'La cantidad por producto debe estar entre 1 y 20'; END IF;

  PERFORM set_config('app.id_actor', p_id_usuario::text, true);

  INSERT INTO orden (id_usuario, id_cafeteria, estado, hora_recogida, codigo_recogida, total, notas)
  VALUES (p_id_usuario, p_id_cafeteria,
          CASE WHEN p_metodo = 'efectivo' THEN 'confirmada'::estado_orden
               ELSE 'pendiente'::estado_orden END,
          p_hora_recogida, lpad((floor(random() * 1000000))::int::text, 6, '0'),
          v_total, NULLIF(btrim(p_notas), ''))
  RETURNING id_orden INTO v_id_orden;

  INSERT INTO detalle_orden (id_orden, id_producto, cantidad, precio_unitario)
  SELECT v_id_orden, p.id_producto, i.cantidad, p.precio
    FROM (SELECT (e->>'id_producto')::uuid AS id_producto, SUM((e->>'cantidad')::int) AS cantidad
            FROM jsonb_array_elements(p_items) e GROUP BY 1) i
    JOIN producto p ON p.id_producto = i.id_producto;

  INSERT INTO pago (id_orden, metodo, monto) VALUES (v_id_orden, p_metodo, v_total);
  RETURN v_id_orden;
END $$;

-- 4.4 Confirmar pago de tarjeta/transferencia (lo llama el backend tras la pasarela)
CREATE FUNCTION sp_confirmar_pago(p_id_orden UUID, p_referencia VARCHAR) RETURNS VOID
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE v_pago pago%ROWTYPE;
BEGIN
  SELECT p.* INTO v_pago FROM pago p JOIN orden o USING (id_orden)
   WHERE p.id_orden = p_id_orden AND o.estado = 'pendiente' FOR UPDATE OF p, o;
  IF NOT FOUND THEN RAISE EXCEPTION 'La orden no existe o ya no está pendiente de pago'; END IF;
  IF v_pago.metodo = 'efectivo' OR v_pago.estado <> 'pendiente' THEN
    RAISE EXCEPTION 'El pago no admite confirmación electrónica';
  END IF;

  UPDATE pago SET estado = 'pagado', referencia = p_referencia, pagado_en = now()
   WHERE id_pago = v_pago.id_pago;
  PERFORM set_config('app.id_actor', '', true);
  UPDATE orden SET estado = 'confirmada' WHERE id_orden = p_id_orden;
END $$;

-- 4.5 La cafetería avanza la orden: en_preparacion | lista | cancelada
CREATE FUNCTION sp_cambiar_estado_orden(p_id_orden UUID, p_nuevo estado_orden, p_id_actor UUID)
RETURNS VOID
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE v_cafe UUID;
BEGIN
  IF p_nuevo NOT IN ('en_preparacion','lista','cancelada') THEN
    RAISE EXCEPTION 'Estado no permitido en este procedimiento: %', p_nuevo;
  END IF;
  SELECT o.id_cafeteria INTO v_cafe FROM orden o WHERE o.id_orden = p_id_orden FOR UPDATE;
  IF NOT FOUND THEN RAISE EXCEPTION 'La orden no existe'; END IF;
  IF NOT EXISTS (
    SELECT 1 FROM usuario u LEFT JOIN cafeteria c ON c.id_cafeteria = v_cafe
     WHERE u.id_usuario = p_id_actor AND u.activo
       AND (u.id_rol = 3 OR (u.id_rol = 2 AND c.id_admin = u.id_usuario))
  ) THEN RAISE EXCEPTION 'Sin permiso para modificar esta orden' USING ERRCODE = 'insufficient_privilege'; END IF;

  PERFORM set_config('app.id_actor', p_id_actor::text, true);
  UPDATE orden SET estado = p_nuevo WHERE id_orden = p_id_orden;
END $$;

-- 4.6 El alumno cancela su orden (solo antes de que se prepare)
CREATE FUNCTION sp_cancelar_orden(p_id_orden UUID, p_id_usuario UUID) RETURNS VOID
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE v_estado estado_orden;
BEGIN
  SELECT estado INTO v_estado FROM orden
   WHERE id_orden = p_id_orden AND id_usuario = p_id_usuario FOR UPDATE;
  IF NOT FOUND THEN RAISE EXCEPTION 'La orden no existe'; END IF;
  IF v_estado NOT IN ('pendiente','confirmada') THEN
    RAISE EXCEPTION 'La orden ya está en preparación y no se puede cancelar';
  END IF;
  PERFORM set_config('app.id_actor', p_id_usuario::text, true);
  UPDATE orden SET estado = 'cancelada' WHERE id_orden = p_id_orden;
END $$;

-- 4.7 Entrega: valida el código del alumno; el efectivo se cobra aquí
CREATE FUNCTION sp_entregar_orden(p_id_orden UUID, p_codigo VARCHAR, p_id_actor UUID)
RETURNS VOID
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE v_o orden%ROWTYPE;
BEGIN
  SELECT * INTO v_o FROM orden WHERE id_orden = p_id_orden FOR UPDATE;
  IF NOT FOUND THEN RAISE EXCEPTION 'La orden no existe'; END IF;
  IF NOT EXISTS (
    SELECT 1 FROM usuario u LEFT JOIN cafeteria c ON c.id_cafeteria = v_o.id_cafeteria
     WHERE u.id_usuario = p_id_actor AND u.activo
       AND (u.id_rol = 3 OR (u.id_rol = 2 AND c.id_admin = u.id_usuario))
  ) THEN RAISE EXCEPTION 'Sin permiso para entregar esta orden' USING ERRCODE = 'insufficient_privilege'; END IF;
  IF v_o.estado <> 'lista' THEN RAISE EXCEPTION 'La orden no está lista para entrega'; END IF;
  IF v_o.codigo_recogida <> p_codigo THEN RAISE EXCEPTION 'Código de recogida incorrecto'; END IF;

  UPDATE pago SET estado = 'pagado', pagado_en = now()
   WHERE id_orden = p_id_orden AND metodo = 'efectivo' AND estado = 'pendiente';
  PERFORM set_config('app.id_actor', p_id_actor::text, true);
  UPDATE orden SET estado = 'entregada' WHERE id_orden = p_id_orden;
END $$;


-- ---------------------------------------------------------------------
-- ---> changelog/migrations/V003__vistas_y_seguridad.sql
-- ---------------------------------------------------------------------
-- =====================================================================
-- CampusBite · TEC-03 · Migración V003: vistas y seguridad
-- Vistas de consulta y permisos mínimos para el rol app_backend.
-- Origen: campusbite_TEC-02.sql (PostgreSQL 14+). Se aplica en una sola
-- transacción por Liquibase (ver changelog-master.xml).
-- =====================================================================

-- 5. Vistas de consulta --------------------------------------------------
CREATE VIEW vw_menu_cafeteria AS
SELECT c.id_cafeteria, c.nombre AS cafeteria, c.es_del_campus, c.punto_entrega,
       cat.nombre AS categoria, p.id_producto, p.nombre AS producto,
       p.descripcion, p.precio, p.imagen_url
  FROM producto p
  JOIN cafeteria c ON c.id_cafeteria = p.id_cafeteria AND c.activa
  LEFT JOIN categoria cat ON cat.id_categoria = p.id_categoria
 WHERE p.disponible;

CREATE VIEW vw_cola_cafeteria AS      -- pantalla de la cafetería (sin código de recogida)
SELECT o.id_orden, o.id_cafeteria, o.estado, o.hora_recogida, o.notas, u.nombre AS alumno,
       jsonb_agg(jsonb_build_object('producto', p.nombre, 'cantidad', d.cantidad)
                 ORDER BY p.nombre) AS productos,
       o.total, pg.metodo, pg.estado AS estado_pago
  FROM orden o
  JOIN usuario u        ON u.id_usuario = o.id_usuario
  JOIN detalle_orden d  ON d.id_orden = o.id_orden
  JOIN producto p       ON p.id_producto = d.id_producto
  JOIN pago pg          ON pg.id_orden = o.id_orden
 WHERE o.estado IN ('confirmada','en_preparacion','lista')
 GROUP BY o.id_orden, u.nombre, pg.metodo, pg.estado;

CREATE VIEW vw_mis_ordenes AS         -- filtrar siempre por id_usuario
SELECT o.id_orden, o.id_usuario, c.nombre AS cafeteria, c.punto_entrega, o.estado,
       o.hora_recogida, o.codigo_recogida, o.total, pg.metodo, pg.estado AS estado_pago, o.creada_en
  FROM orden o
  JOIN cafeteria c ON c.id_cafeteria = o.id_cafeteria
  JOIN pago pg     ON pg.id_orden = o.id_orden;

-- 6. Seguridad: el backend solo ejecuta procedimientos y lee vistas -----
DO $$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_backend') THEN
    CREATE ROLE app_backend NOLOGIN;   -- en producción: LOGIN PASSWORD '...'
  END IF;
END $$;

REVOKE ALL ON ALL TABLES    IN SCHEMA public FROM PUBLIC;
REVOKE ALL ON ALL FUNCTIONS IN SCHEMA public FROM PUBLIC;
GRANT USAGE ON SCHEMA public TO app_backend;
GRANT SELECT ON vw_menu_cafeteria, vw_cola_cafeteria, vw_mis_ordenes TO app_backend;
GRANT EXECUTE ON FUNCTION
  sp_registrar_usuario(VARCHAR,VARCHAR,VARCHAR,VARCHAR,VARCHAR),
  sp_obtener_credenciales(VARCHAR),
  sp_crear_orden(UUID,UUID,TIMESTAMPTZ,metodo_pago,JSONB,VARCHAR),
  sp_confirmar_pago(UUID,VARCHAR),
  sp_cambiar_estado_orden(UUID,estado_orden,UUID),
  sp_cancelar_orden(UUID,UUID),
  sp_entregar_orden(UUID,VARCHAR,UUID)
TO app_backend;


-- Mejora TEC-03: las funciones que se creen en migraciones futuras tampoco
-- serán ejecutables por PUBLIC (REVOKE ... ON ALL FUNCTIONS solo afecta a las
-- ya existentes). Cada migración nueva debe hacer su propio GRANT a app_backend.
ALTER DEFAULT PRIVILEGES IN SCHEMA public REVOKE EXECUTE ON FUNCTIONS FROM PUBLIC;


-- ---------------------------------------------------------------------
-- ---> changelog/migrations/V004__endurecimiento_seguridad.sql
-- ---------------------------------------------------------------------
-- =====================================================================
-- CampusBite · V004 · Endurecimiento de seguridad de la base de datos
-- Modelo de confianza: el backend autentica a la persona y pasa su id; la BD
-- solo expone procedimientos (SECURITY DEFINER) y NUNCA tablas al rol de la API.
-- =====================================================================

-- 1. Nadie crea objetos en el esquema público ni entra a la BD por defecto
REVOKE CREATE ON SCHEMA public FROM PUBLIC;

DO $$
BEGIN
  EXECUTE format('REVOKE ALL ON DATABASE %I FROM PUBLIC', current_database());
  EXECUTE format('GRANT CONNECT ON DATABASE %I TO app_backend', current_database());
EXCEPTION WHEN insufficient_privilege THEN
  RAISE WARNING 'No se pudo ajustar los permisos de la base (no eres su dueño); hazlo manualmente.';
END $$;

-- 2. Límites del rol de la API: una consulta o transacción colgada no tumba el servicio
ALTER ROLE app_backend CONNECTION LIMIT 50;
ALTER ROLE app_backend SET statement_timeout = '10s';
ALTER ROLE app_backend SET lock_timeout = '3s';
ALTER ROLE app_backend SET idle_in_transaction_session_timeout = '15s';

-- 3. Funciones de trigger con search_path fijo (evita suplantación de objetos)
ALTER FUNCTION fn_set_actualizado_en()        SET search_path = public, pg_temp;
ALTER FUNCTION fn_validar_transicion_orden()  SET search_path = public, pg_temp;
ALTER FUNCTION fn_historial_orden()           SET search_path = public, pg_temp;
ALTER FUNCTION fn_cancelar_pago()             SET search_path = public, pg_temp;
ALTER FUNCTION fn_validar_detalle()           SET search_path = public, pg_temp;

-- 4. Código de recogida con aleatoriedad criptográfica (antes random(), predecible)
CREATE OR REPLACE FUNCTION sp_crear_orden(
  p_id_usuario UUID, p_id_cafeteria UUID, p_hora_recogida TIMESTAMPTZ,
  p_metodo metodo_pago, p_items JSONB, p_notas VARCHAR DEFAULT NULL
) RETURNS UUID
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_tz       CONSTANT TEXT := 'America/Mexico_City';
  v_local    TIMESTAMP;
  v_n_items  INT; v_n_validos INT; v_cant_ok BOOLEAN;
  v_total    NUMERIC(10,2);
  v_id_orden UUID;
  v_cafe     cafeteria%ROWTYPE;
  v_acepta   BOOLEAN;
BEGIN
  IF NOT EXISTS (SELECT 1 FROM usuario WHERE id_usuario = p_id_usuario AND id_rol = 1 AND activo) THEN
    RAISE EXCEPTION 'Usuario no válido para realizar órdenes';
  END IF;

  SELECT * INTO v_cafe FROM cafeteria WHERE id_cafeteria = p_id_cafeteria AND activa;
  IF NOT FOUND THEN RAISE EXCEPTION 'La cafetería no existe o no está activa'; END IF;

  v_acepta := CASE p_metodo WHEN 'efectivo' THEN v_cafe.acepta_efectivo
                            WHEN 'tarjeta'  THEN v_cafe.acepta_tarjeta
                            ELSE v_cafe.acepta_transferencia END;
  IF NOT v_acepta THEN
    RAISE EXCEPTION 'La cafetería no acepta el método de pago %', p_metodo;
  END IF;

  IF p_hora_recogida < now() + interval '5 minutes' THEN
    RAISE EXCEPTION 'La hora de recogida debe ser al menos 5 minutos en el futuro';
  END IF;
  v_local := p_hora_recogida AT TIME ZONE v_tz;
  IF NOT EXISTS (
    SELECT 1 FROM horario_cafeteria h
     WHERE h.id_cafeteria = p_id_cafeteria
       AND h.dia_semana = EXTRACT(ISODOW FROM v_local)
       AND v_local::time BETWEEN h.hora_apertura AND h.hora_cierre
  ) THEN
    RAISE EXCEPTION 'La cafetería está cerrada a la hora de recogida solicitada';
  END IF;

  IF jsonb_typeof(p_items) <> 'array' OR jsonb_array_length(p_items) = 0 THEN
    RAISE EXCEPTION 'La orden debe incluir al menos un producto';
  END IF;

  WITH items AS (
    SELECT (e->>'id_producto')::uuid AS id_producto, SUM((e->>'cantidad')::int) AS cantidad
      FROM jsonb_array_elements(p_items) e GROUP BY 1
  )
  SELECT count(*), count(p.id_producto), COALESCE(bool_and(i.cantidad BETWEEN 1 AND 20), false),
         SUM(i.cantidad * p.precio)
    INTO v_n_items, v_n_validos, v_cant_ok, v_total
    FROM items i
    LEFT JOIN producto p ON p.id_producto = i.id_producto
                        AND p.id_cafeteria = p_id_cafeteria AND p.disponible;

  IF v_n_items <> v_n_validos THEN
    RAISE EXCEPTION 'Hay productos inexistentes, no disponibles o de otra cafetería';
  END IF;
  IF NOT v_cant_ok THEN RAISE EXCEPTION 'La cantidad por producto debe estar entre 1 y 20'; END IF;

  PERFORM set_config('app.id_actor', p_id_usuario::text, true);

  INSERT INTO orden (id_usuario, id_cafeteria, estado, hora_recogida, codigo_recogida, total, notas)
  VALUES (p_id_usuario, p_id_cafeteria,
          CASE WHEN p_metodo = 'efectivo' THEN 'confirmada'::estado_orden
               ELSE 'pendiente'::estado_orden END,
          p_hora_recogida, lpad((('x' || substr(replace(gen_random_uuid()::text, '-', ''), 1, 8))::bit(32)::bigint % 1000000)::text, 6, '0'),
          v_total, NULLIF(btrim(p_notas), ''))
  RETURNING id_orden INTO v_id_orden;

  INSERT INTO detalle_orden (id_orden, id_producto, cantidad, precio_unitario)
  SELECT v_id_orden, p.id_producto, i.cantidad, p.precio
    FROM (SELECT (e->>'id_producto')::uuid AS id_producto, SUM((e->>'cantidad')::int) AS cantidad
            FROM jsonb_array_elements(p_items) e GROUP BY 1) i
    JOIN producto p ON p.id_producto = i.id_producto;

  INSERT INTO pago (id_orden, metodo, monto) VALUES (v_id_orden, p_metodo, v_total);
  RETURN v_id_orden;
END $$;

-- 5. Lecturas con autorización dentro de la BD.
--    Las vistas con datos por persona dejan de ser accesibles para la API:
--    ahora el filtro por dueño es obligatorio (un descuido del backend no filtra datos ajenos).
REVOKE SELECT ON vw_mis_ordenes, vw_cola_cafeteria FROM app_backend;

-- plpgsql (no sql): el plan se cachea entre llamadas; medido 5x más rápido con 300 mil órdenes
CREATE FUNCTION sp_mis_ordenes(p_id_usuario UUID, p_limite INT DEFAULT 20, p_desplazamiento INT DEFAULT 0)
RETURNS SETOF vw_mis_ordenes
LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  RETURN QUERY
    SELECT * FROM vw_mis_ordenes
     WHERE id_usuario = p_id_usuario
     ORDER BY creada_en DESC
     LIMIT LEAST(GREATEST(p_limite, 1), 100) OFFSET GREATEST(p_desplazamiento, 0);
END $$;

CREATE FUNCTION sp_cola_cafeteria(p_id_cafeteria UUID, p_id_actor UUID)
RETURNS SETOF vw_cola_cafeteria
LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM usuario u LEFT JOIN cafeteria c ON c.id_cafeteria = p_id_cafeteria
     WHERE u.id_usuario = p_id_actor AND u.activo
       AND (u.id_rol = 3 OR (u.id_rol = 2 AND c.id_admin = u.id_usuario))
  ) THEN
    RAISE EXCEPTION 'Sin permiso para ver la cola de esta cafetería' USING ERRCODE = 'insufficient_privilege';
  END IF;
  RETURN QUERY SELECT * FROM vw_cola_cafeteria WHERE id_cafeteria = p_id_cafeteria ORDER BY hora_recogida;
END $$;

GRANT EXECUTE ON FUNCTION sp_mis_ordenes(UUID,INT,INT), sp_cola_cafeteria(UUID,UUID) TO app_backend;


-- ---------------------------------------------------------------------
-- ---> changelog/migrations/V005__depuracion_indices.sql
-- ---------------------------------------------------------------------
-- =====================================================================
-- CampusBite · V005 · Depuración de índices (basada en mediciones)
-- Criterio: se queda un índice solo si lo usa una consulta real del sistema o
-- respalda una restricción. Medido con 300 mil órdenes y pg_stat_user_indexes.
--
--  idx_horario_cafeteria_dia  redundante: el UNIQUE (id_cafeteria, dia_semana, hora_apertura)
--                             ya sirve las mismas búsquedas por prefijo.
--  idx_usuario_rol            la columna tiene 3 valores (rol): cero escaneos, nunca selectivo.
--  idx_cafeteria_activa       tabla de decenas de filas; el barrido secuencial es más rápido.
--  idx_cafeteria_admin        igual; las validaciones de permiso entran por la PK de cafeteria.
--  idx_producto_categoria     tabla pequeña y categorías casi nunca se borran (FK SET NULL).
--
-- Se conservan, por uso comprobado: idx_orden_usuario_fecha (mis órdenes),
-- idx_orden_cola_cafeteria (parcial, cola de la cafetería), idx_producto_cafeteria
-- (parcial, menú), idx_historial_orden_fecha (línea de tiempo), idx_detalle_producto
-- (FK de la tabla más grande), idx_push_usuario (envío de notificaciones).
-- =====================================================================
DROP INDEX IF EXISTS idx_horario_cafeteria_dia;
DROP INDEX IF EXISTS idx_usuario_rol;
DROP INDEX IF EXISTS idx_cafeteria_activa;
DROP INDEX IF EXISTS idx_cafeteria_admin;
DROP INDEX IF EXISTS idx_producto_categoria;


-- ---------------------------------------------------------------------
-- ---> consolidado/dev/01_login_app_backend_dev.sql
-- ---------------------------------------------------------------------
-- JJ-Sprint2 09/10/2026: login de la API en desarrollo local (mismo que el changeSet "dev-login-app-backend" de Liquibase).
-- La API se conecta con: Username=app_backend;Password=app_backend_dev. NUNCA usar esta clave fuera de local.
ALTER ROLE app_backend WITH LOGIN PASSWORD 'app_backend_dev';


-- ---------------------------------------------------------------------
-- ---> changelog/seeds/seed_dev.sql
-- ---------------------------------------------------------------------
-- =====================================================================
-- CampusBite · TEC-03 · Datos de prueba (SOLO desarrollo, nunca producción)
-- Idempotente: se puede ejecutar varias veces.
-- Los hashes son marcadores: no sirven para iniciar sesión. El backend
-- debe generar hashes reales (bcrypt/argon2) al registrar usuarios.
-- =====================================================================
INSERT INTO usuario (id_usuario, id_rol, nombre, correo, contrasena_hash, matricula, telefono) VALUES
  ('00000000-0000-0000-0000-0000000000a1', 1, 'Alumno de Prueba',   'alumno@ejemplo.test',   'HASH_DE_PRUEBA_NO_VALIDO', 'UTT0000001', '7710000001'),
  ('00000000-0000-0000-0000-0000000000a2', 2, 'Encargado Cafetería','cafeteria@ejemplo.test','HASH_DE_PRUEBA_NO_VALIDO', NULL,         '7710000002'),
  ('00000000-0000-0000-0000-0000000000a3', 3, 'Admin Sistema',      'admin@ejemplo.test',    'HASH_DE_PRUEBA_NO_VALIDO', NULL,         NULL)
ON CONFLICT DO NOTHING;

INSERT INTO cafeteria (id_cafeteria, id_admin, nombre, ubicacion, es_del_campus, punto_entrega) VALUES
  ('00000000-0000-0000-0000-0000000000c1', '00000000-0000-0000-0000-0000000000a2',
   'Cafetería Central (prueba)', 'Edificio principal, planta baja', true, 'Puerta 1')
ON CONFLICT DO NOTHING;

-- Lunes a domingo, 07:00-20:00, para poder probar órdenes cualquier día
INSERT INTO horario_cafeteria (id_cafeteria, dia_semana, hora_apertura, hora_cierre)
SELECT '00000000-0000-0000-0000-0000000000c1', d, '07:00', '20:00' FROM generate_series(1,7) d
ON CONFLICT DO NOTHING;

INSERT INTO categoria (id_categoria, id_cafeteria, nombre) VALUES
  ('00000000-0000-0000-0000-0000000000d1', '00000000-0000-0000-0000-0000000000c1', 'Desayunos'),
  ('00000000-0000-0000-0000-0000000000d2', '00000000-0000-0000-0000-0000000000c1', 'Bebidas')
ON CONFLICT DO NOTHING;

INSERT INTO producto (id_producto, id_cafeteria, id_categoria, nombre, descripcion, precio) VALUES
  ('00000000-0000-0000-0000-0000000000e1', '00000000-0000-0000-0000-0000000000c1', '00000000-0000-0000-0000-0000000000d1', 'Chilaquiles verdes', 'Con pollo y crema',  55.00),
  ('00000000-0000-0000-0000-0000000000e2', '00000000-0000-0000-0000-0000000000c1', '00000000-0000-0000-0000-0000000000d1', 'Sándwich de jamón',  NULL,                 38.00),
  ('00000000-0000-0000-0000-0000000000e3', '00000000-0000-0000-0000-0000000000c1', '00000000-0000-0000-0000-0000000000d2', 'Café americano',     '12 oz',              25.00)
ON CONFLICT DO NOTHING;



-- Marca de versión visible con: SELECT obj_description('public'::regnamespace);
COMMENT ON SCHEMA public IS 'CampusBite V1.0.0.001 (2026-10-09 16:12:10 -0600)';

COMMIT;

\echo '>>> CampusBite V1.0.0.001 aplicada correctamente'
