-- JJ-Sprint5 13/11/2026: 1,500 registros de demostración para probar paginación, tablero y estadísticas.
-- NO es parte del consolidado (db/consolidado): se ejecuta a mano, una sola vez, con el dueño de la base.
--
--   psql "<cadena de neondb_owner>" -f db/datos_demo/carga_1500_produccion.sql
--
-- Registros principales (1,500):
--   30 cafeterías · 120 categorías · 585 productos · 150 usuarios (149 alumnos + 1 encargado) · 615 órdenes
--   + filas de apoyo que no cuentan: métodos de pago, horario, detalle y pago de cada orden.
-- Para qué sirve cada parte:
--   * GET /api/cafeterias                          -> 30 cafeterías "Demo NNN" (2 páginas de 20).
--   * GET /api/cafeterias/{Demo 001}/productos     -> 150 productos (8 páginas de 20).
--   * GET /api/ordenes/mias (alumno001)            -> 120 órdenes (3 páginas de 50).
--   * Tablero y estadísticas de "Demo 001"         -> 12 órdenes activas hoy + historial de 60 días.
-- Seguridad:
--   * Todo es identificable: correos @demo.campusbite.test (dominio inexistente), cafeterías "Demo NNN · ..." e ids
--     fijos (md5 de 'cb-demo-...'). db/datos_demo/limpiar_1500_produccion.sql lo borra sin tocar datos reales.
--   * Las cuentas demo NO pueden iniciar sesión (hash inválido). Para usar una, ponle contraseña con
--     POST /api/auth/restablecer-password (ya nacen con el correo verificado).
--   * Una sola transacción: si algo falla no queda nada a medias. Si ya está cargado, se detiene sin cambiar nada.
\set ON_ERROR_STOP on

BEGIN;

DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM cafeteria WHERE id_cafeteria = md5('cb-demo-cafeteria-1')::uuid) THEN
    RAISE EXCEPTION 'Los datos demo ya están cargados. Si quieres recargarlos, ejecuta primero limpiar_1500_produccion.sql';
  END IF;
END $$;

-- 30 cafeterías activas ---------------------------------------------------------------------------------------
INSERT INTO cafeteria (id_cafeteria, nombre, ubicacion, es_del_campus, punto_entrega, activa)
SELECT md5('cb-demo-cafeteria-' || i)::uuid,
       format('Demo %s · %s', lpad(i::text, 3, '0'),
              (ARRAY['Cafetería', 'Antojitos', 'Café', 'Comedor', 'Snacks'])[1 + i % 5]),
       format('Edificio %s, planta baja', chr(65 + (i - 1) % 12)),
       i % 4 <> 0,
       format('Barra %s', i),
       true
  FROM generate_series(1, 30) i;

-- Métodos de pago: las múltiplos de 5 solo aceptan efectivo
INSERT INTO cafeteria_metodo_pago (id_cafeteria, metodo)
SELECT md5('cb-demo-cafeteria-' || i)::uuid, m::metodo_pago
  FROM generate_series(1, 30) i
 CROSS JOIN unnest(ARRAY['efectivo', 'tarjeta', 'transferencia']) m
 WHERE i % 5 <> 0 OR m = 'efectivo';

-- Horario 07:00 a 20:00; las múltiplos de 6 no abren en domingo
INSERT INTO horario_cafeteria (id_cafeteria, dia_semana, hora_apertura, hora_cierre)
SELECT md5('cb-demo-cafeteria-' || i)::uuid, d, '07:00', '20:00'
  FROM generate_series(1, 30) i
 CROSS JOIN generate_series(1, 7) d
 WHERE NOT (i % 6 = 0 AND d = 7);

-- 120 categorías (4 por cafetería) ----------------------------------------------------------------------------
INSERT INTO categoria (id_categoria, id_cafeteria, nombre)
SELECT md5('cb-demo-categoria-' || i || '-' || k)::uuid,
       md5('cb-demo-cafeteria-' || i)::uuid,
       (ARRAY['Desayunos', 'Comidas', 'Bebidas', 'Postres'])[k]
  FROM generate_series(1, 30) i
 CROSS JOIN generate_series(1, 4) k
-- JJ-Sprint5 15/11/2026: desde V1.0.0.013 el trigger ya crea las categorías base al crear la cafetería
ON CONFLICT DO NOTHING;

-- 585 productos: 150 en Demo 001 y 15 en cada una de las otras 29 --------------------------------------------
INSERT INTO producto (id_producto, id_cafeteria, id_categoria, nombre, descripcion, precio, disponible)
SELECT md5('cb-demo-producto-' || c || '-' || p)::uuid,
       md5('cb-demo-cafeteria-' || c)::uuid,
       -- por nombre, no por id: si el trigger creó la categoría, tiene su propio id
       (SELECT k.id_categoria FROM categoria k
         WHERE k.id_cafeteria = md5('cb-demo-cafeteria-' || c)::uuid
           AND k.nombre = (ARRAY['Desayunos', 'Comidas', 'Bebidas', 'Postres'])[1 + (p - 1) % 4]),
       format('%s %s',
              (ARRAY[['Chilaquiles', 'Molletes', 'Hotcakes', 'Enchiladas'],
                     ['Torta', 'Burrito', 'Ensalada', 'Pasta'],
                     ['Café americano', 'Latte', 'Agua de horchata', 'Jugo de naranja'],
                     ['Pay de queso', 'Brownie', 'Gelatina', 'Fruta picada']])[1 + (p - 1) % 4][1 + ((p - 1) / 4) % 4],
              lpad(p::text, 3, '0')),
       'Producto de demostración para probar la paginación.',
       (15 + (p * 7 + c * 3) % 90)::numeric(8, 2),
       p % 13 <> 0                                   -- algunos no disponibles, para probar el filtro
  FROM generate_series(1, 30) c
 CROSS JOIN LATERAL generate_series(1, CASE WHEN c = 1 THEN 150 ELSE 15 END) p;

-- 150 usuarios: 149 alumnos y el encargado de Demo 001 (sin contraseña válida) ------------------------------
INSERT INTO usuario (id_usuario, id_rol, id_cafeteria, nombre, correo, contrasena_hash, matricula, telefono,
                     activo, correo_verificado)
SELECT md5('cb-demo-alumno-' || i)::uuid, 1, NULL,
       format('Alumno Demo %s', lpad(i::text, 3, '0')),
       format('alumno%s@demo.campusbite.test', lpad(i::text, 3, '0')),
       'DEMO_SIN_CONTRASENA',
       format('DEMO%s', lpad(i::text, 4, '0')),
       format('77100%s', lpad(i::text, 5, '0')),
       true, true
  FROM generate_series(1, 149) i;

INSERT INTO usuario (id_usuario, id_rol, id_cafeteria, nombre, correo, contrasena_hash, telefono, activo, correo_verificado)
VALUES (md5('cb-demo-encargado-1')::uuid, 2, md5('cb-demo-cafeteria-1')::uuid, 'Encargado Demo 001',
        'encargado001@demo.campusbite.test', 'DEMO_SIN_CONTRASENA', '7710099999', true, true);

-- 615 órdenes -------------------------------------------------------------------------------------------------
--   1..120   -> alumno001 (para paginar "Mis pedidos")
--   121..603 -> repartidas entre los alumnos 2..149, en los últimos 60 días (entregadas; 1 de cada 11, cancelada)
--   604..615 -> activas HOY en Demo 001 (tablero): pendiente, confirmada, en preparación y lista
CREATE TEMP TABLE demo_orden ON COMMIT DROP AS
SELECT i,
       md5('cb-demo-orden-' || i)::uuid                                              AS id_orden,
       md5('cb-demo-alumno-' || CASE WHEN i <= 120 THEN 1 ELSE 2 + i % 148 END)::uuid AS id_usuario,
       CASE WHEN i > 603 THEN 1 ELSE 1 + (i * 7) % 30 END                              AS c,
       CASE WHEN i > 603 THEN now() - interval '1 minute' * (i - 600)
            ELSE now() - interval '1 day' * (1 + i % 60) - interval '1 hour' * (i % 9) END AS creado_en,
       CASE WHEN i > 603 THEN (ARRAY['pendiente', 'confirmada', 'en_preparacion', 'lista'])[1 + i % 4]
            WHEN i % 11 = 0 THEN 'cancelada'
            ELSE 'entregada' END::estado_orden                                         AS estado,
       CASE WHEN i > 603 AND i % 4 = 0 THEN 'transferencia'
            ELSE (ARRAY['efectivo', 'tarjeta', 'transferencia'])[1 + i % 3] END        AS metodo_txt
  FROM generate_series(1, 615) i;

-- Las cafeterías múltiplos de 5 solo aceptan efectivo
UPDATE demo_orden SET metodo_txt = 'efectivo' WHERE c % 5 = 0;

INSERT INTO orden (id_orden, id_usuario, id_cafeteria, estado, recogida_en, codigo_recogida, notas,
                   motivo_cancelacion, creado_en)
SELECT o.id_orden, o.id_usuario, md5('cb-demo-cafeteria-' || o.c)::uuid, o.estado,
       o.creado_en + interval '30 minutes',
       lpad(((o.i * 7919) % 1000000)::text, 6, '0'),
       CASE WHEN o.i % 7 = 0 THEN 'Sin cebolla, por favor (demo)' END,
       CASE WHEN o.estado = 'cancelada' THEN 'Se nos acabó un producto (demo)' END,
       o.creado_en
  FROM demo_orden o;

-- 1 a 3 productos por orden, de la misma cafetería
INSERT INTO detalle_orden (id_orden, id_producto, cantidad, precio_unitario)
SELECT o.id_orden, p.id_producto, 1 + (o.i + k) % 3, p.precio
  FROM demo_orden o
 CROSS JOIN LATERAL generate_series(1, 1 + o.i % 3) k
  JOIN producto p ON p.id_producto =
       md5('cb-demo-producto-' || o.c || '-' ||
           (1 + (o.i + k * 5) % CASE WHEN o.c = 1 THEN 150 ELSE 15 END))::uuid;

-- Un pago por orden con el total del detalle (lo revisa trg_pago_monto al hacer COMMIT).
-- creado_en = el de la orden: ck_pago_creado exige pagado_en >= creado_en
INSERT INTO pago (id_orden, metodo, monto, estado, pagado_en, creado_en)
SELECT o.id_orden, o.metodo_txt::metodo_pago, t.total,
       CASE WHEN o.estado = 'entregada' THEN 'pagado'
            WHEN o.estado = 'cancelada' THEN 'cancelado'
            ELSE 'pendiente' END::estado_pago,
       CASE WHEN o.estado = 'entregada' THEN o.creado_en + interval '30 minutes' END,
       o.creado_en
  FROM demo_orden o
  JOIN (SELECT d.id_orden, sum(d.subtotal) AS total FROM detalle_orden d GROUP BY d.id_orden) t
    ON t.id_orden = o.id_orden;

-- Resumen ------------------------------------------------------------------------------------------------------
DO $$
DECLARE
  v_caf INT; v_cat INT; v_prod INT; v_usr INT; v_ord INT;
BEGIN
  SELECT count(*) INTO v_caf  FROM cafeteria WHERE nombre LIKE 'Demo ___ · %';
  SELECT count(*) INTO v_cat  FROM categoria c JOIN cafeteria f ON f.id_cafeteria = c.id_cafeteria WHERE f.nombre LIKE 'Demo ___ · %';
  SELECT count(*) INTO v_prod FROM producto p JOIN cafeteria f ON f.id_cafeteria = p.id_cafeteria WHERE f.nombre LIKE 'Demo ___ · %';
  SELECT count(*) INTO v_usr  FROM usuario WHERE correo LIKE '%@demo.campusbite.test';
  SELECT count(*) INTO v_ord  FROM orden o JOIN usuario u ON u.id_usuario = o.id_usuario WHERE u.correo LIKE '%@demo.campusbite.test';
  RAISE NOTICE 'Demo cargada: % cafeterías, % categorías, % productos, % usuarios, % órdenes = % registros',
    v_caf, v_cat, v_prod, v_usr, v_ord, v_caf + v_cat + v_prod + v_usr + v_ord;
END $$;

COMMIT;
