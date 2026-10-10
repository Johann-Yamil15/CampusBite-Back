-- =====================================================================
-- CampusBite · Script consolidado de base de datos
-- Versión : V1.0.0.002
-- Fecha   : 2026-10-10 09:51:31 -0600
-- Autor   : Joseph Yañez
-- Huella  : 49b85978b645
-- Incluye : 48 scripts (ver orden.txt de cada módulo)
--   - scripts/01_base/001_extensiones.sql
--   - scripts/01_base/002_tipos.sql
--   - scripts/01_base/003_rol_app_backend.sql
--   - scripts/02_tablas/001_rol.sql
--   - scripts/02_tablas/002_usuario.sql
--   - scripts/02_tablas/003_cafeteria.sql
--   - scripts/02_tablas/004_horario_cafeteria.sql
--   - scripts/02_tablas/005_categoria.sql
--   - scripts/02_tablas/006_producto.sql
--   - scripts/02_tablas/007_orden.sql
--   - scripts/02_tablas/008_detalle_orden.sql
--   - scripts/02_tablas/009_pago.sql
--   - scripts/02_tablas/010_historial_estado_orden.sql
--   - scripts/02_tablas/011_suscripcion_push.sql
--   - scripts/02_tablas/012_indices.sql
--   - scripts/03_cambios_sprint2/000_retirar_objetos_v1.sql
--   - scripts/03_cambios_sprint2/001_depurar_indices.sql
--   - scripts/03_cambios_sprint2/002_metodos_pago_cafeteria.sql
--   - scripts/03_cambios_sprint2/003_personal_cafeteria.sql
--   - scripts/03_cambios_sprint2/004_quitar_total_orden.sql
--   - scripts/03_cambios_sprint2/005_fechas.sql
--   - scripts/04_triggers/001_actualizado_en.sql
--   - scripts/04_triggers/002_maquina_estados_orden.sql
--   - scripts/04_triggers/003_historial_orden.sql
--   - scripts/04_triggers/004_cancelar_pago.sql
--   - scripts/04_triggers/005_validar_detalle.sql
--   - scripts/04_triggers/006_cafeteria_inmutable.sql
--   - scripts/04_triggers/007_validar_monto_pago.sql
--   - scripts/05_vistas/001_vw_menu_cafeteria.sql
--   - scripts/05_vistas/002_vw_metodos_pago_cafeteria.sql
--   - scripts/05_vistas/003_vw_cola_cafeteria.sql
--   - scripts/05_vistas/004_vw_mis_ordenes.sql
--   - scripts/06_procedimientos/001_sp_exigir_gestor_cafeteria.sql
--   - scripts/06_procedimientos/002_sp_registrar_usuario.sql
--   - scripts/06_procedimientos/003_sp_obtener_credenciales.sql
--   - scripts/06_procedimientos/004_sp_menu_cafeteria.sql
--   - scripts/06_procedimientos/005_sp_crear_orden.sql
--   - scripts/06_procedimientos/006_sp_confirmar_pago.sql
--   - scripts/06_procedimientos/007_sp_cambiar_estado_orden.sql
--   - scripts/06_procedimientos/008_sp_cancelar_orden.sql
--   - scripts/06_procedimientos/009_sp_entregar_orden.sql
--   - scripts/06_procedimientos/010_sp_mis_ordenes.sql
--   - scripts/06_procedimientos/011_sp_cola_cafeteria.sql
--   - scripts/07_seguridad/001_privilegios.sql
--   - scripts/07_seguridad/002_limites_rol_api.sql
--   - scripts/07_seguridad/003_rls.sql
--   - scripts/90_datos_dev/001_login_app_backend.sql
--   - scripts/90_datos_dev/002_datos_prueba.sql
--
-- Idempotente: se puede ejecutar sobre una base vacía o sobre una versión anterior; repetirlo no
-- falla ni duplica nada. Corre todo en una transacción: si algo falla, no queda nada a medias.
-- SOLO PARA DESARROLLO LOCAL (trae la clave pública de app_backend y datos de prueba):
--   psql -U postgres -d campusbite -f campusbite_V1.0.0.002.sql
-- =====================================================================
\set ON_ERROR_STOP on
SET client_encoding = 'UTF8';

BEGIN;

-- ---------------------------------------------------------------------
-- ---> scripts/01_base/001_extensiones.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: citext guarda el correo sin distinguir mayúsculas.
-- En Azure Database for PostgreSQL hay que permitirla antes en el parámetro azure.extensions.
CREATE EXTENSION IF NOT EXISTS citext;


-- ---------------------------------------------------------------------
-- ---> scripts/01_base/002_tipos.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: tipos ENUM. CREATE TYPE no tiene IF NOT EXISTS, por eso va en un bloque DO.
-- Para agregar un valor nuevo a un ENUM: ALTER TYPE ... ADD VALUE IF NOT EXISTS, en un script nuevo.
DO $$
BEGIN
  IF to_regtype('public.estado_orden') IS NULL THEN
    CREATE TYPE estado_orden AS ENUM
      ('pendiente','confirmada','en_preparacion','lista','entregada','cancelada');
  END IF;

  IF to_regtype('public.metodo_pago') IS NULL THEN
    CREATE TYPE metodo_pago AS ENUM ('tarjeta','transferencia','efectivo');
  END IF;

  IF to_regtype('public.estado_pago') IS NULL THEN
    CREATE TYPE estado_pago AS ENUM
      ('pendiente','pagado','rechazado','reembolsado','cancelado');
  END IF;
END $$;


-- ---------------------------------------------------------------------
-- ---> scripts/01_base/003_rol_app_backend.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: rol con el que se conecta la API. Nace sin LOGIN: la clave se asigna fuera del repo
-- (en local la pone 90_datos_dev/001_login_app_backend.sql).
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'app_backend') THEN
    CREATE ROLE app_backend NOLOGIN;
  END IF;
END $$;


-- ---------------------------------------------------------------------
-- ---> scripts/02_tablas/001_rol.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: catálogo de roles (el id coincide con el enum RolUsuario del backend)
CREATE TABLE IF NOT EXISTS rol (
  id_rol  SMALLINT    PRIMARY KEY,
  nombre  VARCHAR(30) NOT NULL UNIQUE
);

INSERT INTO rol (id_rol, nombre) VALUES
  (1,'alumno'), (2,'admin_cafeteria'), (3,'admin_sistema')
ON CONFLICT (id_rol) DO NOTHING;


-- ---------------------------------------------------------------------
-- ---> scripts/02_tablas/002_usuario.sql
-- ---------------------------------------------------------------------
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


-- ---------------------------------------------------------------------
-- ---> scripts/02_tablas/003_cafeteria.sql
-- ---------------------------------------------------------------------
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


-- ---------------------------------------------------------------------
-- ---> scripts/02_tablas/004_horario_cafeteria.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: horario semanal. Las horas son hora local del campus (America/Mexico_City).
CREATE TABLE IF NOT EXISTS horario_cafeteria (
  id_horario    UUID     PRIMARY KEY DEFAULT gen_random_uuid(),
  id_cafeteria  UUID     NOT NULL REFERENCES cafeteria(id_cafeteria) ON DELETE CASCADE,
  dia_semana    SMALLINT NOT NULL CHECK (dia_semana BETWEEN 1 AND 7),  -- ISO: 1 = lunes
  hora_apertura TIME     NOT NULL,
  hora_cierre   TIME     NOT NULL,
  CHECK (hora_cierre > hora_apertura),
  UNIQUE (id_cafeteria, dia_semana, hora_apertura)
);


-- ---------------------------------------------------------------------
-- ---> scripts/02_tablas/005_categoria.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: categorías del menú de cada cafetería
CREATE TABLE IF NOT EXISTS categoria (
  id_categoria UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
  id_cafeteria UUID        NOT NULL REFERENCES cafeteria(id_cafeteria) ON DELETE CASCADE,
  nombre       VARCHAR(60) NOT NULL CHECK (length(btrim(nombre)) >= 2),
  UNIQUE (id_cafeteria, nombre)
);


-- ---------------------------------------------------------------------
-- ---> scripts/02_tablas/006_producto.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: productos del menú
CREATE TABLE IF NOT EXISTS producto (
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


-- ---------------------------------------------------------------------
-- ---> scripts/02_tablas/007_orden.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: órdenes. total, hora_recogida, creada_en y actualizado_en se ajustan en
-- 03_cambios_sprint2 (004_quitar_total_orden.sql y 005_fechas.sql).
CREATE TABLE IF NOT EXISTS orden (
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


-- ---------------------------------------------------------------------
-- ---> scripts/02_tablas/008_detalle_orden.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: renglones de la orden. subtotal es columna generada: no puede desincronizarse.
CREATE TABLE IF NOT EXISTS detalle_orden (
  id_detalle      UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
  id_orden        UUID          NOT NULL REFERENCES orden(id_orden) ON DELETE CASCADE,
  id_producto     UUID          NOT NULL REFERENCES producto(id_producto),
  cantidad        SMALLINT      NOT NULL CHECK (cantidad BETWEEN 1 AND 20),
  precio_unitario NUMERIC(8,2)  NOT NULL CHECK (precio_unitario > 0),
  subtotal        NUMERIC(10,2) GENERATED ALWAYS AS (cantidad * precio_unitario) STORED,
  UNIQUE (id_orden, id_producto)
);


-- ---------------------------------------------------------------------
-- ---> scripts/02_tablas/009_pago.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: pago de la orden (uno por orden). monto es el total cobrado.
CREATE TABLE IF NOT EXISTS pago (
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


-- ---------------------------------------------------------------------
-- ---> scripts/02_tablas/010_historial_estado_orden.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: bitácora de estados de cada orden (la llena un trigger, ver 04_triggers)
CREATE TABLE IF NOT EXISTS historial_estado_orden (
  id_historial UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
  id_orden     UUID         NOT NULL REFERENCES orden(id_orden) ON DELETE CASCADE,
  estado       estado_orden NOT NULL,
  cambiado_por UUID         REFERENCES usuario(id_usuario),
  cambiado_en  TIMESTAMPTZ  NOT NULL DEFAULT now()
);


-- ---------------------------------------------------------------------
-- ---> scripts/02_tablas/011_suscripcion_push.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: suscripciones a notificaciones push del navegador
CREATE TABLE IF NOT EXISTS suscripcion_push (
  id_suscripcion UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
  id_usuario     UUID         NOT NULL REFERENCES usuario(id_usuario) ON DELETE CASCADE,
  endpoint       TEXT         NOT NULL UNIQUE,
  llave_p256dh   VARCHAR(255) NOT NULL,
  llave_auth     VARCHAR(255) NOT NULL,
  creada_en      TIMESTAMPTZ  NOT NULL DEFAULT now()
);


-- ---------------------------------------------------------------------
-- ---> scripts/02_tablas/012_indices.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: índices con uso comprobado (medidos con 300 mil órdenes, ver db/README.md).
-- PostgreSQL no indexa las FK por su cuenta. Los 5 índices que V005 quitó ya no se crean.
-- No se usa CREATE INDEX IF NOT EXISTS: PostgreSQL valida las columnas aunque el índice ya exista, y
-- 03_cambios_sprint2/005_fechas.sql renombra creada_en y hora_recogida (el índice las sigue solo).
DO $$
DECLARE
  r RECORD;
BEGIN
  FOR r IN
    SELECT v.nombre, v.definicion
      FROM (VALUES
        ('idx_producto_cafeteria',    'ON producto(id_cafeteria, id_categoria) WHERE disponible'),
        ('idx_orden_usuario_fecha',   'ON orden(id_usuario, creada_en DESC)'),
        ('idx_orden_cola_cafeteria',  'ON orden(id_cafeteria, hora_recogida) WHERE estado IN (''confirmada'',''en_preparacion'',''lista'')'),
        ('idx_detalle_producto',      'ON detalle_orden(id_producto)'),        -- (id_orden) lo cubre el UNIQUE
        ('idx_historial_orden_fecha', 'ON historial_estado_orden(id_orden, cambiado_en)'),
        ('idx_push_usuario',          'ON suscripcion_push(id_usuario)')
      ) AS v(nombre, definicion)
  LOOP
    IF to_regclass('public.' || r.nombre) IS NULL THEN
      EXECUTE format('CREATE INDEX %I %s', r.nombre, r.definicion);
    END IF;
  END LOOP;
END $$;


-- ---------------------------------------------------------------------
-- ---> scripts/03_cambios_sprint2/000_retirar_objetos_v1.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: retira lo que V1.0.0.001 creó y Sprint 2 rehace de otra forma.
--  * Los sp_* eran FUNCTION; ahora son PROCEDURE (06_procedimientos). Una función y un procedimiento
--    no pueden convivir con el mismo nombre y argumentos, así que se borran las funciones sp_* que queden.
--    No se usa DROP FUNCTION IF EXISTS: falla con "is not a function" si ya existe el procedimiento.
--  * Las vistas se recrean en 05_vistas. Se quitan aquí porque leen columnas que 03_cambios_sprint2
--    elimina o renombra (orden.total, hora_recogida, creada_en) y PostgreSQL no lo permitiría.
DO $$
DECLARE
  v_funcion REGPROCEDURE;
BEGIN
  FOR v_funcion IN
    SELECT p.oid::regprocedure
      FROM pg_proc p
     WHERE p.pronamespace = 'public'::regnamespace
       AND p.prokind = 'f'
       AND p.proname LIKE 'sp\_%'
  LOOP
    EXECUTE format('DROP FUNCTION %s', v_funcion);
  END LOOP;
END $$;

DROP VIEW IF EXISTS vw_mis_ordenes, vw_cola_cafeteria, vw_menu_cafeteria;


-- ---------------------------------------------------------------------
-- ---> scripts/03_cambios_sprint2/001_depurar_indices.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: índices que V005 quitó por redundantes o sin uso (ver db/README.md).
-- En una base nueva ya no existen; en una de V1.0.0.001 se borran.
DROP INDEX IF EXISTS idx_horario_cafeteria_dia;   -- lo cubre el UNIQUE (id_cafeteria, dia_semana, hora_apertura)
DROP INDEX IF EXISTS idx_usuario_rol;             -- 3 valores posibles: nunca es selectivo
DROP INDEX IF EXISTS idx_cafeteria_activa;        -- tabla de decenas de filas
DROP INDEX IF EXISTS idx_cafeteria_admin;         -- la columna id_admin desaparece en 003_personal_cafeteria.sql
DROP INDEX IF EXISTS idx_producto_categoria;      -- tabla pequeña; las categorías casi nunca se borran


-- ---------------------------------------------------------------------
-- ---> scripts/03_cambios_sprint2/002_metodos_pago_cafeteria.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: acepta_efectivo, acepta_tarjeta y acepta_transferencia eran un grupo repetido
-- (una columna por cada valor de metodo_pago). Agregar un método obligaba a agregar otra columna.
-- Ahora cada método aceptado es una fila. La validación del método vive en sp_crear_orden.
CREATE TABLE IF NOT EXISTS cafeteria_metodo_pago (
  id_cafeteria UUID        NOT NULL REFERENCES cafeteria(id_cafeteria) ON DELETE CASCADE,
  metodo       metodo_pago NOT NULL,
  PRIMARY KEY (id_cafeteria, metodo)
);

DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.columns
              WHERE table_schema = 'public' AND table_name = 'cafeteria' AND column_name = 'acepta_efectivo') THEN
    INSERT INTO cafeteria_metodo_pago (id_cafeteria, metodo)
    SELECT c.id_cafeteria, m.metodo
      FROM cafeteria c
     CROSS JOIN LATERAL (VALUES ('efectivo'::metodo_pago, c.acepta_efectivo),
                                ('tarjeta'::metodo_pago, c.acepta_tarjeta),
                                ('transferencia'::metodo_pago, c.acepta_transferencia)) AS m(metodo, acepta)
     WHERE m.acepta
    ON CONFLICT (id_cafeteria, metodo) DO NOTHING;

    -- El CHECK (acepta_efectivo OR ...) se borra junto con sus columnas.
    ALTER TABLE cafeteria
      DROP COLUMN acepta_efectivo,
      DROP COLUMN acepta_tarjeta,
      DROP COLUMN acepta_transferencia;
  END IF;
END $$;


-- ---------------------------------------------------------------------
-- ---> scripts/03_cambios_sprint2/003_personal_cafeteria.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: corrige el círculo usuario -> orden -> cafeteria -> usuario.
-- Antes "quién administra una cafetería" se guardaba dos veces: usuario.id_rol = 2 y cafeteria.id_admin,
-- sin nada que las uniera. Podía quedar una cafetería administrada por un alumno, o un encargado sin
-- cafetería, y las validaciones de permisos tenían que revisar las dos cosas.
-- Ahora la relación vive en un solo lugar: usuario.id_cafeteria, y un CHECK de la misma fila garantiza
-- que solo un admin_cafeteria (rol 2) la tenga. cafeteria ya no apunta a usuario: la FK va en un solo
-- sentido. Además una cafetería puede tener varios encargados.
ALTER TABLE usuario ADD COLUMN IF NOT EXISTS id_cafeteria UUID REFERENCES cafeteria(id_cafeteria);

DO $$
DECLARE
  v_n INT;
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.columns
              WHERE table_schema = 'public' AND table_name = 'cafeteria' AND column_name = 'id_admin') THEN
    -- Con el modelo nuevo cada encargado pertenece a una sola cafetería.
    SELECT count(*) INTO v_n
      FROM (SELECT c.id_admin FROM cafeteria c
             WHERE c.id_admin IS NOT NULL GROUP BY c.id_admin HAVING count(*) > 1) d;
    IF v_n > 0 THEN
      RAISE EXCEPTION '% usuario(s) administran más de una cafetería. Deja una sola por usuario antes de migrar.', v_n;
    END IF;

    UPDATE usuario u
       SET id_cafeteria = c.id_cafeteria
      FROM cafeteria c
     WHERE c.id_admin = u.id_usuario AND u.id_rol = 2;

    -- El dato inconsistente que permitía el modelo anterior: no se migra, se avisa.
    SELECT count(*) INTO v_n
      FROM cafeteria c JOIN usuario u ON u.id_usuario = c.id_admin
     WHERE u.id_rol <> 2;
    IF v_n > 0 THEN
      RAISE WARNING '% cafetería(s) tenían como id_admin a un usuario que no es admin_cafeteria; quedan sin encargado.', v_n;
    END IF;

    ALTER TABLE cafeteria DROP COLUMN id_admin;
  END IF;

  IF NOT EXISTS (SELECT 1 FROM pg_constraint
                  WHERE conrelid = 'usuario'::regclass AND conname = 'ck_usuario_cafeteria_solo_encargado') THEN
    ALTER TABLE usuario ADD CONSTRAINT ck_usuario_cafeteria_solo_encargado
      CHECK (id_cafeteria IS NULL OR id_rol = 2);
  END IF;
END $$;

COMMENT ON COLUMN usuario.id_cafeteria IS
  'Cafetería donde trabaja el encargado (solo rol 2). NULL para alumnos y admin_sistema.';


-- ---------------------------------------------------------------------
-- ---> scripts/03_cambios_sprint2/004_quitar_total_orden.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: orden.total era un dato derivado (la suma de detalle_orden.subtotal) y además
-- se repetía en pago.monto. Se queda un solo total guardado: pago.monto, que es lo que se cobra.
-- La regla pago.monto = suma del detalle la vigila 04_triggers/007_validar_monto_pago.sql.
-- Las vistas que leían orden.total se quitaron en 000_retirar_objetos_v1.sql y vuelven en 05_vistas.
ALTER TABLE orden DROP COLUMN IF EXISTS total;


-- ---------------------------------------------------------------------
-- ---> scripts/03_cambios_sprint2/005_fechas.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: revisión de fechas.
-- Convención: TIMESTAMPTZ (instante, se guarda en UTC) termina en _en; TIME (hora local) empieza con hora_;
-- DATE (día sin hora) empieza con fecha_.
--  1. Nombres: orden.creada_en y suscripcion_push.creada_en -> creado_en (igual que el resto de tablas).
--     orden.hora_recogida no es una hora sino un instante completo -> recogida_en.
--  2. orden.actualizado_en era redundante: en una orden solo cambia el estado, y cada cambio ya queda con
--     su fecha en historial_estado_orden. Se borra (con su trigger).
--  3. No se agrega usuario.fecha_registro: sería creado_en::date guardado dos veces. Si hace falta el día,
--     se calcula: (creado_en AT TIME ZONE 'America/Mexico_City')::date.
--  4. Reglas de orden entre fechas que antes no existían (CHECK).
DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM information_schema.columns
              WHERE table_schema = 'public' AND table_name = 'orden' AND column_name = 'creada_en') THEN
    ALTER TABLE orden RENAME COLUMN creada_en TO creado_en;
  END IF;
  IF EXISTS (SELECT 1 FROM information_schema.columns
              WHERE table_schema = 'public' AND table_name = 'suscripcion_push' AND column_name = 'creada_en') THEN
    ALTER TABLE suscripcion_push RENAME COLUMN creada_en TO creado_en;
  END IF;
  IF EXISTS (SELECT 1 FROM information_schema.columns
              WHERE table_schema = 'public' AND table_name = 'orden' AND column_name = 'hora_recogida') THEN
    ALTER TABLE orden RENAME COLUMN hora_recogida TO recogida_en;
  END IF;
END $$;

DROP TRIGGER IF EXISTS trg_orden_upd ON orden;
ALTER TABLE orden DROP COLUMN IF EXISTS actualizado_en;

DO $$
DECLARE
  r RECORD;
BEGIN
  FOR r IN
    SELECT v.tabla, v.nombre, v.regla
      FROM (VALUES
        ('usuario',   'ck_usuario_fechas',   'actualizado_en >= creado_en'),
        ('cafeteria', 'ck_cafeteria_fechas', 'actualizado_en >= creado_en'),
        ('producto',  'ck_producto_fechas',  'actualizado_en >= creado_en'),
        ('orden',     'ck_orden_recogida',   'recogida_en > creado_en'),
        ('pago',      'ck_pago_fechas',      'pagado_en <= actualizado_en')
      ) AS v(tabla, nombre, regla)
  LOOP
    IF NOT EXISTS (SELECT 1 FROM pg_constraint
                    WHERE conrelid = r.tabla::regclass AND conname = r.nombre) THEN
      EXECUTE format('ALTER TABLE %I ADD CONSTRAINT %I CHECK (%s)', r.tabla, r.nombre, r.regla);
    END IF;
  END LOOP;
END $$;

COMMENT ON COLUMN orden.recogida_en IS 'Instante en que el alumno recoge la orden (TIMESTAMPTZ, UTC).';
COMMENT ON COLUMN horario_cafeteria.hora_apertura IS 'Hora local del campus (America/Mexico_City).';
COMMENT ON COLUMN horario_cafeteria.hora_cierre   IS 'Hora local del campus (America/Mexico_City).';


-- ---------------------------------------------------------------------
-- ---> scripts/04_triggers/001_actualizado_en.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: mantiene actualizado_en. orden ya no lo tiene (ver 03_cambios_sprint2/005_fechas.sql).
CREATE OR REPLACE FUNCTION fn_set_actualizado_en() RETURNS trigger
LANGUAGE plpgsql SET search_path = public, pg_temp AS $$
BEGIN
  NEW.actualizado_en := now();
  RETURN NEW;
END $$;

CREATE OR REPLACE TRIGGER trg_usuario_upd   BEFORE UPDATE ON usuario   FOR EACH ROW EXECUTE FUNCTION fn_set_actualizado_en();
CREATE OR REPLACE TRIGGER trg_cafeteria_upd BEFORE UPDATE ON cafeteria FOR EACH ROW EXECUTE FUNCTION fn_set_actualizado_en();
CREATE OR REPLACE TRIGGER trg_producto_upd  BEFORE UPDATE ON producto  FOR EACH ROW EXECUTE FUNCTION fn_set_actualizado_en();
CREATE OR REPLACE TRIGGER trg_pago_upd      BEFORE UPDATE ON pago      FOR EACH ROW EXECUTE FUNCTION fn_set_actualizado_en();


-- ---------------------------------------------------------------------
-- ---> scripts/04_triggers/002_maquina_estados_orden.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: máquina de estados de la orden
CREATE OR REPLACE FUNCTION fn_validar_transicion_orden() RETURNS trigger
LANGUAGE plpgsql SET search_path = public, pg_temp AS $$
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

CREATE OR REPLACE TRIGGER trg_orden_transicion BEFORE UPDATE OF estado ON orden
  FOR EACH ROW EXECUTE FUNCTION fn_validar_transicion_orden();


-- ---------------------------------------------------------------------
-- ---> scripts/04_triggers/003_historial_orden.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: bitácora automática de estados. Quién hizo el cambio llega en app.id_actor,
-- que fijan los procedimientos con set_config(..., true) (vale solo dentro de la transacción).
CREATE OR REPLACE FUNCTION fn_historial_orden() RETURNS trigger
LANGUAGE plpgsql SET search_path = public, pg_temp AS $$
BEGIN
  INSERT INTO historial_estado_orden (id_orden, estado, cambiado_por)
  VALUES (NEW.id_orden, NEW.estado,
          NULLIF(current_setting('app.id_actor', true), '')::uuid);
  RETURN NEW;
END $$;

CREATE OR REPLACE TRIGGER trg_orden_historial_ins AFTER INSERT ON orden
  FOR EACH ROW EXECUTE FUNCTION fn_historial_orden();
CREATE OR REPLACE TRIGGER trg_orden_historial_upd AFTER UPDATE OF estado ON orden
  FOR EACH ROW WHEN (OLD.estado IS DISTINCT FROM NEW.estado)
  EXECUTE FUNCTION fn_historial_orden();


-- ---------------------------------------------------------------------
-- ---> scripts/04_triggers/004_cancelar_pago.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: al cancelar una orden se cierra su pago (reembolso si ya estaba pagado)
CREATE OR REPLACE FUNCTION fn_cancelar_pago() RETURNS trigger
LANGUAGE plpgsql SET search_path = public, pg_temp AS $$
BEGIN
  UPDATE pago
     SET estado    = CASE WHEN estado = 'pagado' THEN 'reembolsado'::estado_pago
                          ELSE 'cancelado'::estado_pago END,
         pagado_en = CASE WHEN estado = 'pagado' THEN pagado_en ELSE NULL END
   WHERE id_orden = NEW.id_orden AND estado IN ('pendiente','pagado');
  RETURN NEW;
END $$;

CREATE OR REPLACE TRIGGER trg_orden_cancelada AFTER UPDATE OF estado ON orden
  FOR EACH ROW WHEN (NEW.estado = 'cancelada' AND OLD.estado <> 'cancelada')
  EXECUTE FUNCTION fn_cancelar_pago();


-- ---------------------------------------------------------------------
-- ---> scripts/04_triggers/005_validar_detalle.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: un detalle solo puede llevar productos de la cafetería de su orden
-- (orden.id_cafeteria = producto.id_cafeteria). Junto con 006_cafeteria_inmutable.sql cierra el otro
-- círculo del modelo: orden -> cafeteria <- producto <- detalle_orden -> orden.
CREATE OR REPLACE FUNCTION fn_validar_detalle() RETURNS trigger
LANGUAGE plpgsql SET search_path = public, pg_temp AS $$
BEGIN
  IF (SELECT p.id_cafeteria FROM producto p WHERE p.id_producto = NEW.id_producto)
     IS DISTINCT FROM
     (SELECT o.id_cafeteria FROM orden o WHERE o.id_orden = NEW.id_orden) THEN
    RAISE EXCEPTION 'El producto no pertenece a la cafetería de la orden'
      USING ERRCODE = 'check_violation';
  END IF;
  RETURN NEW;
END $$;

CREATE OR REPLACE TRIGGER trg_detalle_valida BEFORE INSERT OR UPDATE ON detalle_orden
  FOR EACH ROW EXECUTE FUNCTION fn_validar_detalle();


-- ---------------------------------------------------------------------
-- ---> scripts/04_triggers/006_cafeteria_inmutable.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: orden.id_cafeteria se podría deducir de sus productos, pero se guarda porque la
-- orden necesita su cafetería antes de tener renglones (horario, método de pago) y porque la cola de la
-- cafetería se busca por ella (idx_orden_cola_cafeteria). Para que esa copia no se desincronice:
-- fn_validar_detalle revisa cada renglón al insertarlo, y este trigger impide cambiar después la
-- cafetería de una orden o de un producto (eso rompería los renglones ya validados).
CREATE OR REPLACE FUNCTION fn_bloquear_cambio_cafeteria() RETURNS trigger
LANGUAGE plpgsql SET search_path = public, pg_temp AS $$
BEGIN
  IF NEW.id_cafeteria IS DISTINCT FROM OLD.id_cafeteria THEN
    RAISE EXCEPTION 'No se puede cambiar la cafetería de % (crea uno nuevo)', TG_TABLE_NAME
      USING ERRCODE = 'check_violation';
  END IF;
  RETURN NEW;
END $$;

CREATE OR REPLACE TRIGGER trg_orden_cafeteria_fija BEFORE UPDATE OF id_cafeteria ON orden
  FOR EACH ROW EXECUTE FUNCTION fn_bloquear_cambio_cafeteria();
CREATE OR REPLACE TRIGGER trg_producto_cafeteria_fija BEFORE UPDATE OF id_cafeteria ON producto
  FOR EACH ROW EXECUTE FUNCTION fn_bloquear_cambio_cafeteria();


-- ---------------------------------------------------------------------
-- ---> scripts/04_triggers/007_validar_monto_pago.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: pago.monto es el único total guardado (orden.total se quitó). Esta regla garantiza
-- que siempre sea igual a la suma de detalle_orden.subtotal.
-- Es un CONSTRAINT TRIGGER diferido: se revisa al hacer COMMIT, cuando la orden ya tiene detalle y pago.
-- CREATE OR REPLACE no existe para constraint triggers, por eso se borran y se crean.
CREATE OR REPLACE FUNCTION fn_validar_monto_pago() RETURNS trigger
LANGUAGE plpgsql SET search_path = public, pg_temp AS $$
DECLARE
  v_id_orden UUID := CASE WHEN TG_OP = 'DELETE' THEN OLD.id_orden ELSE NEW.id_orden END;
  v_monto    NUMERIC(10,2);
  v_suma     NUMERIC(10,2);
BEGIN
  SELECT p.monto INTO v_monto FROM pago p WHERE p.id_orden = v_id_orden;
  IF NOT FOUND THEN RETURN NULL; END IF;     -- la orden se borró (cascada) o no tiene pago

  SELECT COALESCE(sum(d.subtotal), 0) INTO v_suma FROM detalle_orden d WHERE d.id_orden = v_id_orden;
  IF v_monto <> v_suma THEN
    RAISE EXCEPTION 'El monto del pago (%) no coincide con el detalle de la orden (%)', v_monto, v_suma
      USING ERRCODE = 'check_violation';
  END IF;
  RETURN NULL;
END $$;

DROP TRIGGER IF EXISTS trg_pago_monto ON pago;
CREATE CONSTRAINT TRIGGER trg_pago_monto
  AFTER INSERT OR UPDATE OF monto ON pago
  DEFERRABLE INITIALLY DEFERRED
  FOR EACH ROW EXECUTE FUNCTION fn_validar_monto_pago();

DROP TRIGGER IF EXISTS trg_detalle_monto ON detalle_orden;
CREATE CONSTRAINT TRIGGER trg_detalle_monto
  AFTER INSERT OR UPDATE OR DELETE ON detalle_orden
  DEFERRABLE INITIALLY DEFERRED
  FOR EACH ROW EXECUTE FUNCTION fn_validar_monto_pago();


-- ---------------------------------------------------------------------
-- ---> scripts/05_vistas/001_vw_menu_cafeteria.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: productos disponibles de las cafeterías activas (lo usa sp_menu_cafeteria)
DROP VIEW IF EXISTS vw_menu_cafeteria;
CREATE VIEW vw_menu_cafeteria AS
SELECT c.id_cafeteria, c.nombre AS cafeteria, c.es_del_campus, c.punto_entrega,
       cat.nombre AS categoria, p.id_producto, p.nombre AS producto,
       p.descripcion, p.precio, p.imagen_url
  FROM producto p
  JOIN cafeteria c        ON c.id_cafeteria = p.id_cafeteria AND c.activa
  LEFT JOIN categoria cat ON cat.id_categoria = p.id_categoria
 WHERE p.disponible;


-- ---------------------------------------------------------------------
-- ---> scripts/05_vistas/002_vw_metodos_pago_cafeteria.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: métodos de pago de cada cafetería activa, en una fila (reemplaza a acepta_*)
DROP VIEW IF EXISTS vw_metodos_pago_cafeteria;
CREATE VIEW vw_metodos_pago_cafeteria AS
SELECT c.id_cafeteria, array_agg(m.metodo ORDER BY m.metodo) AS metodos
  FROM cafeteria c
  JOIN cafeteria_metodo_pago m ON m.id_cafeteria = c.id_cafeteria
 WHERE c.activa
 GROUP BY c.id_cafeteria;


-- ---------------------------------------------------------------------
-- ---> scripts/05_vistas/003_vw_cola_cafeteria.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: pantalla de pedidos de la cafetería (sin código de recogida). Lo usa sp_cola_cafeteria.
-- total sale de pago.monto: es el único total guardado (ver 03_cambios_sprint2/004_quitar_total_orden.sql).
DROP VIEW IF EXISTS vw_cola_cafeteria;
CREATE VIEW vw_cola_cafeteria AS
SELECT o.id_orden, o.id_cafeteria, o.estado, o.recogida_en, o.notas, u.nombre AS alumno,
       jsonb_agg(jsonb_build_object('producto', p.nombre, 'cantidad', d.cantidad)
                 ORDER BY p.nombre) AS productos,
       pg.monto AS total, pg.metodo, pg.estado AS estado_pago
  FROM orden o
  JOIN usuario u       ON u.id_usuario = o.id_usuario
  JOIN detalle_orden d ON d.id_orden = o.id_orden
  JOIN producto p      ON p.id_producto = d.id_producto
  JOIN pago pg         ON pg.id_orden = o.id_orden
 WHERE o.estado IN ('confirmada','en_preparacion','lista')
 GROUP BY o.id_orden, u.nombre, pg.monto, pg.metodo, pg.estado;


-- ---------------------------------------------------------------------
-- ---> scripts/05_vistas/004_vw_mis_ordenes.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: historial de órdenes del alumno. Lo usa sp_mis_ordenes, que siempre filtra por id_usuario.
-- total sale de pago.monto (un renglón por orden): no hay que sumar el detalle en cada consulta.
DROP VIEW IF EXISTS vw_mis_ordenes;
CREATE VIEW vw_mis_ordenes AS
SELECT o.id_orden, o.id_usuario, c.nombre AS cafeteria, c.punto_entrega, o.estado,
       o.recogida_en, o.codigo_recogida, pg.monto AS total, pg.metodo, pg.estado AS estado_pago, o.creado_en
  FROM orden o
  JOIN cafeteria c ON c.id_cafeteria = o.id_cafeteria
  JOIN pago pg     ON pg.id_orden = o.id_orden;


-- ---------------------------------------------------------------------
-- ---> scripts/06_procedimientos/001_sp_exigir_gestor_cafeteria.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: interno (la API no lo ejecuta). Falla si el actor no es el encargado de la
-- cafetería ni admin_sistema. Lo usan sp_cambiar_estado_orden, sp_entregar_orden y sp_cola_cafeteria.
CREATE OR REPLACE PROCEDURE sp_exigir_gestor_cafeteria(
  IN p_id_cafeteria UUID,
  IN p_id_actor     UUID,
  IN p_accion       VARCHAR
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM usuario u
     WHERE u.id_usuario = p_id_actor AND u.activo
       AND (u.id_rol = 3 OR (u.id_rol = 2 AND u.id_cafeteria = p_id_cafeteria))
  ) THEN
    RAISE EXCEPTION 'Sin permiso para %', p_accion USING ERRCODE = 'insufficient_privilege';
  END IF;
END $$;


-- ---------------------------------------------------------------------
-- ---> scripts/06_procedimientos/002_sp_registrar_usuario.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: registro de alumno. Devuelve el id nuevo en p_id_usuario.
--   CALL sp_registrar_usuario('Ana López', 'ana@utt.edu.mx', '<hash bcrypt>', 'UTT123', NULL, NULL);
-- matrícula y teléfono son opcionales: se mandan NULL.
CREATE OR REPLACE PROCEDURE sp_registrar_usuario(
  IN  p_nombre          VARCHAR,
  IN  p_correo          VARCHAR,
  IN  p_contrasena_hash VARCHAR,
  IN  p_matricula       VARCHAR,
  IN  p_telefono        VARCHAR,
  OUT p_id_usuario      UUID
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  INSERT INTO usuario (id_rol, nombre, correo, contrasena_hash, matricula, telefono)
  VALUES (1, btrim(p_nombre), p_correo, p_contrasena_hash, p_matricula, p_telefono)
  RETURNING id_usuario INTO p_id_usuario;
EXCEPTION WHEN unique_violation THEN
  RAISE EXCEPTION 'El correo o la matrícula ya están registrados' USING ERRCODE = 'unique_violation';
END $$;


-- ---------------------------------------------------------------------
-- ---> scripts/06_procedimientos/003_sp_obtener_credenciales.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: datos para el login (el hash se compara en el backend con BCrypt).
--   CALL sp_obtener_credenciales('ana@utt.edu.mx', NULL, NULL, NULL, NULL);
-- Si el correo no existe, los cuatro valores regresan NULL.
CREATE OR REPLACE PROCEDURE sp_obtener_credenciales(
  IN  p_correo          VARCHAR,
  OUT p_id_usuario      UUID,
  OUT p_id_rol          SMALLINT,
  OUT p_contrasena_hash VARCHAR,
  OUT p_activo          BOOLEAN
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  SELECT u.id_usuario, u.id_rol, u.contrasena_hash, u.activo
    INTO p_id_usuario, p_id_rol, p_contrasena_hash, p_activo
    FROM usuario u
   WHERE u.correo = p_correo::citext;
END $$;


-- ---------------------------------------------------------------------
-- ---> scripts/06_procedimientos/004_sp_menu_cafeteria.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: menú de una cafetería activa (o de todas si p_id_cafeteria es NULL), con sus métodos
-- de pago. Reemplaza la lectura directa de vw_menu_cafeteria que tenía la API.
--   CALL sp_menu_cafeteria(NULL, NULL);
-- p_menu: [{"id_cafeteria", "cafeteria", "es_del_campus", "punto_entrega", "metodos_pago": [...],
--           "productos": [{"id_producto", "producto", "categoria", "descripcion", "precio", "imagen_url"}]}]
CREATE OR REPLACE PROCEDURE sp_menu_cafeteria(
  IN  p_id_cafeteria UUID,
  OUT p_menu         JSONB
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  SELECT COALESCE(jsonb_agg(jsonb_build_object(
           'id_cafeteria',  c.id_cafeteria,
           'cafeteria',     c.nombre,
           'es_del_campus', c.es_del_campus,
           'punto_entrega', c.punto_entrega,
           'metodos_pago',  COALESCE(to_jsonb(mp.metodos), '[]'::jsonb),
           'productos',     COALESCE(pr.productos, '[]'::jsonb))
         ORDER BY c.nombre), '[]'::jsonb)
    INTO p_menu
    FROM cafeteria c
    LEFT JOIN vw_metodos_pago_cafeteria mp ON mp.id_cafeteria = c.id_cafeteria
    LEFT JOIN LATERAL (
      SELECT jsonb_agg(jsonb_build_object(
               'id_producto', m.id_producto,
               'producto',    m.producto,
               'categoria',   m.categoria,
               'descripcion', m.descripcion,
               'precio',      m.precio,
               'imagen_url',  m.imagen_url)
             ORDER BY m.categoria NULLS LAST, m.producto) AS productos
        FROM vw_menu_cafeteria m
       WHERE m.id_cafeteria = c.id_cafeteria
    ) pr ON true
   WHERE c.activa
     AND (p_id_cafeteria IS NULL OR c.id_cafeteria = p_id_cafeteria);
END $$;


-- ---------------------------------------------------------------------
-- ---> scripts/06_procedimientos/005_sp_crear_orden.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: crea la orden completa (orden + detalle + pago) en una transacción.
--   CALL sp_crear_orden(<id_usuario>, <id_cafeteria>, '2026-10-10 13:00-06', 'efectivo',
--                       '[{"id_producto":"<uuid>","cantidad":2}]', 'Sin cebolla', NULL);
-- Devuelve el id en p_id_orden. Efectivo nace "confirmada"; tarjeta y transferencia, "pendiente".
-- p_notas es opcional: se manda NULL.
CREATE OR REPLACE PROCEDURE sp_crear_orden(
  IN  p_id_usuario   UUID,
  IN  p_id_cafeteria UUID,
  IN  p_recogida_en  TIMESTAMPTZ,
  IN  p_metodo       metodo_pago,
  IN  p_items        JSONB,
  IN  p_notas        VARCHAR,
  OUT p_id_orden     UUID
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_tz        CONSTANT TEXT := 'America/Mexico_City';   -- zona de horario_cafeteria
  v_local     TIMESTAMP;
  v_n_items   INT;
  v_n_validos INT;
  v_cant_ok   BOOLEAN;
  v_total     NUMERIC(10,2);
BEGIN
  IF NOT EXISTS (SELECT 1 FROM usuario u
                  WHERE u.id_usuario = p_id_usuario AND u.id_rol = 1 AND u.activo) THEN
    RAISE EXCEPTION 'Usuario no válido para realizar órdenes';
  END IF;

  IF NOT EXISTS (SELECT 1 FROM cafeteria c WHERE c.id_cafeteria = p_id_cafeteria AND c.activa) THEN
    RAISE EXCEPTION 'La cafetería no existe o no está activa';
  END IF;

  IF NOT EXISTS (SELECT 1 FROM cafeteria_metodo_pago m
                  WHERE m.id_cafeteria = p_id_cafeteria AND m.metodo = p_metodo) THEN
    RAISE EXCEPTION 'La cafetería no acepta el método de pago %', p_metodo;
  END IF;

  IF p_recogida_en < now() + interval '5 minutes' THEN
    RAISE EXCEPTION 'La hora de recogida debe ser al menos 5 minutos en el futuro';
  END IF;
  v_local := p_recogida_en AT TIME ZONE v_tz;
  IF NOT EXISTS (
    SELECT 1 FROM horario_cafeteria h
     WHERE h.id_cafeteria = p_id_cafeteria
       AND h.dia_semana = EXTRACT(ISODOW FROM v_local)
       AND v_local::time BETWEEN h.hora_apertura AND h.hora_cierre
  ) THEN
    RAISE EXCEPTION 'La cafetería está cerrada a la hora de recogida solicitada';
  END IF;

  -- Dos IF separados: jsonb_array_length falla si p_items no es un arreglo.
  IF p_items IS NULL OR jsonb_typeof(p_items) <> 'array' THEN
    RAISE EXCEPTION 'La orden debe incluir al menos un producto';
  END IF;
  IF jsonb_array_length(p_items) = 0 THEN
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
  IF NOT v_cant_ok THEN
    RAISE EXCEPTION 'La cantidad por producto debe estar entre 1 y 20';
  END IF;

  PERFORM set_config('app.id_actor', p_id_usuario::text, true);

  -- Código de recogida con aleatoriedad criptográfica (gen_random_uuid), no con random().
  INSERT INTO orden (id_usuario, id_cafeteria, estado, recogida_en, codigo_recogida, notas)
  VALUES (p_id_usuario, p_id_cafeteria,
          CASE WHEN p_metodo = 'efectivo' THEN 'confirmada'::estado_orden
               ELSE 'pendiente'::estado_orden END,
          p_recogida_en,
          lpad((('x' || substr(replace(gen_random_uuid()::text, '-', ''), 1, 8))::bit(32)::bigint % 1000000)::text, 6, '0'),
          NULLIF(btrim(p_notas), ''))
  RETURNING id_orden INTO p_id_orden;

  INSERT INTO detalle_orden (id_orden, id_producto, cantidad, precio_unitario)
  SELECT p_id_orden, p.id_producto, i.cantidad, p.precio
    FROM (SELECT (e->>'id_producto')::uuid AS id_producto, SUM((e->>'cantidad')::int) AS cantidad
            FROM jsonb_array_elements(p_items) e GROUP BY 1) i
    JOIN producto p ON p.id_producto = i.id_producto;

  INSERT INTO pago (id_orden, metodo, monto) VALUES (p_id_orden, p_metodo, v_total);
END $$;


-- ---------------------------------------------------------------------
-- ---> scripts/06_procedimientos/006_sp_confirmar_pago.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: confirma el pago con tarjeta o transferencia (lo llama el backend tras la pasarela)
--   CALL sp_confirmar_pago(<id_orden>, 'REF-PASARELA-123');
CREATE OR REPLACE PROCEDURE sp_confirmar_pago(
  IN p_id_orden   UUID,
  IN p_referencia VARCHAR
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_id_pago UUID;
  v_metodo  metodo_pago;
  v_estado  estado_pago;
BEGIN
  SELECT p.id_pago, p.metodo, p.estado INTO v_id_pago, v_metodo, v_estado
    FROM pago p JOIN orden o ON o.id_orden = p.id_orden
   WHERE p.id_orden = p_id_orden AND o.estado = 'pendiente'
     FOR UPDATE OF p, o;
  IF NOT FOUND THEN
    RAISE EXCEPTION 'La orden no existe o ya no está pendiente de pago';
  END IF;
  IF v_metodo = 'efectivo' OR v_estado <> 'pendiente' THEN
    RAISE EXCEPTION 'El pago no admite confirmación electrónica';
  END IF;

  UPDATE pago SET estado = 'pagado', referencia = p_referencia, pagado_en = now()
   WHERE id_pago = v_id_pago;
  PERFORM set_config('app.id_actor', '', true);    -- lo confirma el sistema, no una persona
  UPDATE orden SET estado = 'confirmada' WHERE id_orden = p_id_orden;
END $$;


-- ---------------------------------------------------------------------
-- ---> scripts/06_procedimientos/007_sp_cambiar_estado_orden.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: la cafetería avanza la orden a en_preparacion, lista o cancelada.
--   CALL sp_cambiar_estado_orden(<id_orden>, 'en_preparacion', <id_encargado>);
CREATE OR REPLACE PROCEDURE sp_cambiar_estado_orden(
  IN p_id_orden UUID,
  IN p_nuevo    estado_orden,
  IN p_id_actor UUID
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_id_cafeteria UUID;
BEGIN
  IF p_nuevo NOT IN ('en_preparacion','lista','cancelada') THEN
    RAISE EXCEPTION 'Estado no permitido en este procedimiento: %', p_nuevo;
  END IF;

  SELECT o.id_cafeteria INTO v_id_cafeteria FROM orden o WHERE o.id_orden = p_id_orden FOR UPDATE;
  IF NOT FOUND THEN RAISE EXCEPTION 'La orden no existe'; END IF;

  CALL sp_exigir_gestor_cafeteria(v_id_cafeteria, p_id_actor, 'modificar esta orden');

  PERFORM set_config('app.id_actor', p_id_actor::text, true);
  UPDATE orden SET estado = p_nuevo WHERE id_orden = p_id_orden;
END $$;


-- ---------------------------------------------------------------------
-- ---> scripts/06_procedimientos/008_sp_cancelar_orden.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: el alumno cancela su orden (solo antes de que entre a preparación)
--   CALL sp_cancelar_orden(<id_orden>, <id_alumno>);
CREATE OR REPLACE PROCEDURE sp_cancelar_orden(
  IN p_id_orden   UUID,
  IN p_id_usuario UUID
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_estado estado_orden;
BEGIN
  SELECT o.estado INTO v_estado FROM orden o
   WHERE o.id_orden = p_id_orden AND o.id_usuario = p_id_usuario FOR UPDATE;
  IF NOT FOUND THEN RAISE EXCEPTION 'La orden no existe'; END IF;
  IF v_estado NOT IN ('pendiente','confirmada') THEN
    RAISE EXCEPTION 'La orden ya está en preparación y no se puede cancelar';
  END IF;

  PERFORM set_config('app.id_actor', p_id_usuario::text, true);
  UPDATE orden SET estado = 'cancelada' WHERE id_orden = p_id_orden;
END $$;


-- ---------------------------------------------------------------------
-- ---> scripts/06_procedimientos/009_sp_entregar_orden.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: entrega con el código del alumno; el efectivo se cobra aquí.
--   CALL sp_entregar_orden(<id_orden>, '123456', <id_encargado>);
CREATE OR REPLACE PROCEDURE sp_entregar_orden(
  IN p_id_orden UUID,
  IN p_codigo   VARCHAR,
  IN p_id_actor UUID
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_id_cafeteria UUID;
  v_estado       estado_orden;
  v_codigo       CHAR(6);
BEGIN
  SELECT o.id_cafeteria, o.estado, o.codigo_recogida INTO v_id_cafeteria, v_estado, v_codigo
    FROM orden o WHERE o.id_orden = p_id_orden FOR UPDATE;
  IF NOT FOUND THEN RAISE EXCEPTION 'La orden no existe'; END IF;

  CALL sp_exigir_gestor_cafeteria(v_id_cafeteria, p_id_actor, 'entregar esta orden');

  IF v_estado <> 'lista' THEN RAISE EXCEPTION 'La orden no está lista para entrega'; END IF;
  IF v_codigo <> p_codigo THEN RAISE EXCEPTION 'Código de recogida incorrecto'; END IF;

  UPDATE pago SET estado = 'pagado', pagado_en = now()
   WHERE id_orden = p_id_orden AND metodo = 'efectivo' AND estado = 'pendiente';
  PERFORM set_config('app.id_actor', p_id_actor::text, true);
  UPDATE orden SET estado = 'entregada' WHERE id_orden = p_id_orden;
END $$;


-- ---------------------------------------------------------------------
-- ---> scripts/06_procedimientos/010_sp_mis_ordenes.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: historial del alumno, más recientes primero, paginado (máximo 100 por página).
--   CALL sp_mis_ordenes(<id_alumno>, 20, 0, NULL);
-- p_ordenes: [{"id_orden", "cafeteria", "punto_entrega", "estado", "recogida_en", "codigo_recogida",
--              "total", "metodo", "estado_pago", "creado_en"}]
CREATE OR REPLACE PROCEDURE sp_mis_ordenes(
  IN  p_id_usuario     UUID,
  IN  p_limite         INT,
  IN  p_desplazamiento INT,
  OUT p_ordenes        JSONB
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  SELECT COALESCE(jsonb_agg(to_jsonb(m) ORDER BY m.creado_en DESC), '[]'::jsonb)
    INTO p_ordenes
    FROM (SELECT v.id_orden, v.cafeteria, v.punto_entrega, v.estado, v.recogida_en, v.codigo_recogida,
                 v.total, v.metodo, v.estado_pago, v.creado_en
            FROM vw_mis_ordenes v
           WHERE v.id_usuario = p_id_usuario
           ORDER BY v.creado_en DESC
           LIMIT LEAST(GREATEST(COALESCE(p_limite, 20), 1), 100)
          OFFSET GREATEST(COALESCE(p_desplazamiento, 0), 0)) m;
END $$;


-- ---------------------------------------------------------------------
-- ---> scripts/06_procedimientos/011_sp_cola_cafeteria.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: pedidos activos de una cafetería, por hora de recogida. Solo su encargado o admin_sistema.
--   CALL sp_cola_cafeteria(<id_cafeteria>, <id_encargado>, NULL);
-- p_cola: [{"id_orden", "estado", "recogida_en", "notas", "alumno", "productos": [...], "total",
--           "metodo", "estado_pago"}]
CREATE OR REPLACE PROCEDURE sp_cola_cafeteria(
  IN  p_id_cafeteria UUID,
  IN  p_id_actor     UUID,
  OUT p_cola         JSONB
)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
BEGIN
  CALL sp_exigir_gestor_cafeteria(p_id_cafeteria, p_id_actor, 'ver la cola de esta cafetería');

  SELECT COALESCE(jsonb_agg(to_jsonb(q) ORDER BY q.recogida_en), '[]'::jsonb)
    INTO p_cola
    FROM (SELECT v.id_orden, v.estado, v.recogida_en, v.notas, v.alumno, v.productos,
                 v.total, v.metodo, v.estado_pago
            FROM vw_cola_cafeteria v
           WHERE v.id_cafeteria = p_id_cafeteria) q;
END $$;


-- ---------------------------------------------------------------------
-- ---> scripts/07_seguridad/001_privilegios.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: la API (app_backend) solo ejecuta los procedimientos de esta lista. No lee tablas
-- ni vistas. Se quita todo y se vuelve a dar, así el resultado es el mismo sin importar cuántas veces corra.
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
REVOKE ALL ON ALL TABLES    IN SCHEMA public FROM PUBLIC, app_backend;   -- incluye vistas
REVOKE ALL ON ALL SEQUENCES IN SCHEMA public FROM PUBLIC, app_backend;
REVOKE ALL ON ALL ROUTINES  IN SCHEMA public FROM PUBLIC, app_backend;   -- funciones y procedimientos

-- Corrige V003/V004: "ALTER DEFAULT PRIVILEGES IN SCHEMA public REVOKE ... FROM PUBLIC" no hace nada,
-- porque el permiso de PUBLIC viene del valor global y por esquema solo se pueden AGREGAR permisos.
-- Por eso sp_mis_ordenes y sp_cola_cafeteria quedaron ejecutables por cualquier rol. Esta es la forma
-- correcta: sin IN SCHEMA, para lo que cree en adelante el rol que ejecuta los scripts.
ALTER DEFAULT PRIVILEGES REVOKE EXECUTE ON ROUTINES FROM PUBLIC;

GRANT USAGE ON SCHEMA public TO app_backend;
GRANT EXECUTE ON PROCEDURE
  sp_registrar_usuario,
  sp_obtener_credenciales,
  sp_menu_cafeteria,
  sp_crear_orden,
  sp_confirmar_pago,
  sp_cambiar_estado_orden,
  sp_cancelar_orden,
  sp_entregar_orden,
  sp_mis_ordenes,
  sp_cola_cafeteria
TO app_backend;
-- sp_exigir_gestor_cafeteria es interno: no se le da a app_backend.

-- Nadie entra a la base sin permiso explícito. Si quien ejecuta no es dueño de la base, solo se avisa.
DO $$
BEGIN
  EXECUTE format('REVOKE ALL ON DATABASE %I FROM PUBLIC', current_database());
  EXECUTE format('GRANT CONNECT ON DATABASE %I TO app_backend', current_database());
EXCEPTION WHEN insufficient_privilege THEN
  RAISE WARNING 'No se pudieron ajustar los permisos de la base (no eres su dueño); hazlo a mano.';
END $$;


-- ---------------------------------------------------------------------
-- ---> scripts/07_seguridad/002_limites_rol_api.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: una consulta o transacción colgada de la API no tumba el servicio.
-- En servicios administrados (Neon, Azure) quien migra puede no tener permiso sobre el rol: solo se avisa.
DO $$
BEGIN
  ALTER ROLE app_backend CONNECTION LIMIT 50;
  ALTER ROLE app_backend SET statement_timeout = '10s';
  ALTER ROLE app_backend SET lock_timeout = '3s';
  ALTER ROLE app_backend SET idle_in_transaction_session_timeout = '15s';
EXCEPTION WHEN insufficient_privilege THEN
  RAISE WARNING 'No se pudieron fijar los límites de app_backend (falta permiso sobre el rol); hazlo a mano.';
END $$;


-- ---------------------------------------------------------------------
-- ---> scripts/07_seguridad/003_rls.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: Row Level Security como segunda barrera. Sin políticas, un rol que no sea el dueño
-- de la tabla no ve ninguna fila aunque alguien le dé SELECT por error. Los procedimientos son
-- SECURITY DEFINER (corren como el dueño), así que siguen funcionando igual.
-- ENABLE ROW LEVEL SECURITY no falla si ya está activo.
DO $$
DECLARE
  v_tabla TEXT;
BEGIN
  FOREACH v_tabla IN ARRAY ARRAY[
    'rol', 'usuario', 'cafeteria', 'cafeteria_metodo_pago', 'horario_cafeteria', 'categoria', 'producto',
    'orden', 'detalle_orden', 'pago', 'historial_estado_orden', 'suscripcion_push']
  LOOP
    EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY', v_tabla);
  END LOOP;
END $$;


-- ---------------------------------------------------------------------
-- ---> scripts/90_datos_dev/001_login_app_backend.sql
-- ---------------------------------------------------------------------
-- JJ-Sprint2 09/10/2026: login de la API en desarrollo local (mismo que el changeSet "dev-login-app-backend" de Liquibase).
-- La API se conecta con: Username=app_backend;Password=app_backend_dev. NUNCA usar esta clave fuera de local.
ALTER ROLE app_backend WITH LOGIN PASSWORD 'app_backend_dev';


-- ---------------------------------------------------------------------
-- ---> scripts/90_datos_dev/002_datos_prueba.sql
-- ---------------------------------------------------------------------
-- JY-Sprint2 09/10/2026: datos de prueba (SOLO desarrollo, nunca producción). Se puede ejecutar varias veces.
-- Los hashes son marcadores: no sirven para iniciar sesión. El backend genera hashes reales (BCrypt).
-- Orden: primero la cafetería, porque el encargado apunta a ella (usuario.id_cafeteria).
INSERT INTO cafeteria (id_cafeteria, nombre, ubicacion, es_del_campus, punto_entrega) VALUES
  ('00000000-0000-0000-0000-0000000000c1',
   'Cafetería Central (prueba)', 'Edificio principal, planta baja', true, 'Puerta 1')
ON CONFLICT (id_cafeteria) DO NOTHING;

INSERT INTO cafeteria_metodo_pago (id_cafeteria, metodo) VALUES
  ('00000000-0000-0000-0000-0000000000c1', 'efectivo'),
  ('00000000-0000-0000-0000-0000000000c1', 'tarjeta'),
  ('00000000-0000-0000-0000-0000000000c1', 'transferencia')
ON CONFLICT (id_cafeteria, metodo) DO NOTHING;

INSERT INTO usuario (id_usuario, id_rol, id_cafeteria, nombre, correo, contrasena_hash, matricula, telefono) VALUES
  ('00000000-0000-0000-0000-0000000000a1', 1, NULL, 'Alumno de Prueba',    'alumno@ejemplo.test',    'HASH_DE_PRUEBA_NO_VALIDO', 'UTT0000001', '7710000001'),
  ('00000000-0000-0000-0000-0000000000a2', 2, '00000000-0000-0000-0000-0000000000c1',
                                              'Encargado Cafetería', 'cafeteria@ejemplo.test', 'HASH_DE_PRUEBA_NO_VALIDO', NULL,         '7710000002'),
  ('00000000-0000-0000-0000-0000000000a3', 3, NULL, 'Admin Sistema',       'admin@ejemplo.test',     'HASH_DE_PRUEBA_NO_VALIDO', NULL,         NULL)
ON CONFLICT (id_usuario) DO NOTHING;

-- Lunes a domingo, 07:00-20:00, para poder probar órdenes cualquier día
INSERT INTO horario_cafeteria (id_cafeteria, dia_semana, hora_apertura, hora_cierre)
SELECT '00000000-0000-0000-0000-0000000000c1', d, '07:00', '20:00' FROM generate_series(1, 7) d
ON CONFLICT (id_cafeteria, dia_semana, hora_apertura) DO NOTHING;

INSERT INTO categoria (id_categoria, id_cafeteria, nombre) VALUES
  ('00000000-0000-0000-0000-0000000000d1', '00000000-0000-0000-0000-0000000000c1', 'Desayunos'),
  ('00000000-0000-0000-0000-0000000000d2', '00000000-0000-0000-0000-0000000000c1', 'Bebidas')
ON CONFLICT (id_categoria) DO NOTHING;

INSERT INTO producto (id_producto, id_cafeteria, id_categoria, nombre, descripcion, precio) VALUES
  ('00000000-0000-0000-0000-0000000000e1', '00000000-0000-0000-0000-0000000000c1', '00000000-0000-0000-0000-0000000000d1', 'Chilaquiles verdes', 'Con pollo y crema', 55.00),
  ('00000000-0000-0000-0000-0000000000e2', '00000000-0000-0000-0000-0000000000c1', '00000000-0000-0000-0000-0000000000d1', 'Sándwich de jamón',  NULL,                38.00),
  ('00000000-0000-0000-0000-0000000000e3', '00000000-0000-0000-0000-0000000000c1', '00000000-0000-0000-0000-0000000000d2', 'Café americano',     '12 oz',             25.00)
ON CONFLICT (id_producto) DO NOTHING;


-- Marca de versión visible con: SELECT obj_description('public'::regnamespace);
COMMENT ON SCHEMA public IS 'CampusBite V1.0.0.002 (2026-10-10 09:51:31 -0600)';

COMMIT;

\echo '>>> CampusBite V1.0.0.002 aplicada correctamente'
