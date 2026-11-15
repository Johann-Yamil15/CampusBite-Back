-- JJ-Sprint5 13/11/2026: borra TODO lo que creó carga_1500_produccion.sql (y nada más).
--   psql "<cadena de neondb_owner>" -f db/datos_demo/limpiar_1500_produccion.sql
-- Identifica los datos demo por sus ids fijos (cafeterías md5('cb-demo-cafeteria-N')) y por el correo
-- @demo.campusbite.test. Si un alumno real hizo una orden en una cafetería "Demo", esa orden también se borra.
-- Una sola transacción. La bitácora conserva el registro de las altas y las bajas (como cualquier cambio).
\set ON_ERROR_STOP on

BEGIN;

CREATE TEMP TABLE demo_caf ON COMMIT DROP AS
SELECT md5('cb-demo-cafeteria-' || i)::uuid AS id FROM generate_series(1, 30) i;

CREATE TEMP TABLE demo_usr ON COMMIT DROP AS
SELECT id_usuario AS id FROM usuario WHERE correo LIKE '%@demo.campusbite.test';

-- Órdenes de usuarios demo o en cafeterías demo (detalle, pago, historial y reportes se van en cascada)
DELETE FROM orden WHERE id_usuario IN (SELECT id FROM demo_usr) OR id_cafeteria IN (SELECT id FROM demo_caf);

-- Lo que apunta a un usuario demo sin cascada
DELETE FROM historial_estado_orden WHERE cambiado_por IN (SELECT id FROM demo_usr);
DELETE FROM cierre_cafeteria       WHERE creado_por   IN (SELECT id FROM demo_usr);

-- Productos (no tienen cascada desde cafetería) y favoritos de productos demo
DELETE FROM favorito_producto WHERE id_cafeteria IN (SELECT id FROM demo_caf);
DELETE FROM producto          WHERE id_cafeteria IN (SELECT id FROM demo_caf);

-- Usuarios demo (sus favoritos, preferencias y suscripciones se van en cascada)
DELETE FROM usuario WHERE id_usuario IN (SELECT id FROM demo_usr);

-- Cafeterías demo (categorías, métodos de pago, horario, cierres, promociones y favoritos en cascada)
DELETE FROM cafeteria WHERE id_cafeteria IN (SELECT id FROM demo_caf);

DO $$
BEGIN
  RAISE NOTICE 'Datos demo eliminados. Quedan: % cafeterías demo, % usuarios demo',
    (SELECT count(*) FROM cafeteria WHERE id_cafeteria IN (SELECT md5('cb-demo-cafeteria-' || i)::uuid FROM generate_series(1, 30) i)),
    (SELECT count(*) FROM usuario WHERE correo LIKE '%@demo.campusbite.test');
END $$;

COMMIT;
