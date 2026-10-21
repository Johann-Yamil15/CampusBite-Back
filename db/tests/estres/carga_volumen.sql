-- =====================================================================
-- JY-Sprint3 20/10/2026: carga masiva para pruebas de estrés (SOLO en una base de pruebas desechable).
-- Llena la base muy por encima de lo que tendrá el campus, para ver si los procedimientos aguantan.
--   psql -U postgres -d estres -v cafeterias=10000 -v ordenes=500000 -f carga_volumen.sql
-- Requiere superusuario: usa session_replication_role = replica para saltarse los triggers al cargar
-- (si no, la carga tardaría horas). Por eso la bitácora y el historial se generan aparte, con el mismo volumen
-- que tendrían.
-- =====================================================================
\set ON_ERROR_STOP on
\if :{?cafeterias} \else \set cafeterias 10000 \endif
\if :{?ordenes} \else \set ordenes 500000 \endif
\if :{?alumnos} \else \set alumnos 50000 \endif
\timing on

SELECT current_setting('server_version') AS postgres, :cafeterias AS cafeterias, :ordenes AS ordenes, :alumnos AS alumnos;

BEGIN;
SET LOCAL session_replication_role = replica;

-- Cafeterías (10 % inactivas) con 1 a 3 métodos de pago, horario de lunes a domingo, 3 categorías y 20 productos
CREATE TEMP TABLE c AS
SELECT gen_random_uuid() AS id, g AS n FROM generate_series(1, :cafeterias) g;
CREATE UNIQUE INDEX ON c (n);
ANALYZE c;

INSERT INTO cafeteria (id_cafeteria, nombre, ubicacion, es_del_campus, punto_entrega, activa)
SELECT c.id, format('Cafetería Carga %s', lpad(c.n::text, 6, '0')), format('Edificio %s', c.n % 40),
       c.n % 3 <> 0, format('Ventanilla %s', c.n % 5 + 1), c.n % 10 <> 0
  FROM c;

INSERT INTO cafeteria_metodo_pago (id_cafeteria, metodo)
SELECT c.id, m.metodo
  FROM c CROSS JOIN (VALUES (1, 'efectivo'::metodo_pago), (2, 'tarjeta'), (3, 'transferencia')) m(k, metodo)
 WHERE m.k = 1 OR (c.n + m.k) % 3 = 0;

INSERT INTO horario_cafeteria (id_cafeteria, dia_semana, hora_apertura, hora_cierre)
SELECT c.id, d, '07:00', '20:00' FROM c CROSS JOIN generate_series(1, 7) d;

INSERT INTO categoria (id_categoria, id_cafeteria, nombre)
SELECT gen_random_uuid(), c.id, k FROM c CROSS JOIN (VALUES ('Desayunos'), ('Comidas'), ('Bebidas')) v(k);

INSERT INTO producto (id_producto, id_cafeteria, id_categoria, nombre, precio, disponible)
SELECT gen_random_uuid(), c.id,
       (SELECT cat.id_categoria FROM categoria cat WHERE cat.id_cafeteria = c.id ORDER BY cat.nombre OFFSET p % 3 LIMIT 1),
       format('Producto %s', p), 15 + (p * 7) % 80, p % 15 <> 0
  FROM c CROSS JOIN generate_series(1, 20) p;

-- Usuarios: alumnos y un encargado por cafetería
INSERT INTO usuario (id_usuario, id_rol, nombre, correo, contrasena_hash)
SELECT gen_random_uuid(), 1, format('Alumno %s', g), format('alumno%s@carga.test', g), 'HASH' FROM generate_series(1, :alumnos) g;
INSERT INTO usuario (id_usuario, id_rol, id_cafeteria, nombre, correo, contrasena_hash)
SELECT gen_random_uuid(), 2, c.id, format('Encargado %s', c.n), format('encargado%s@carga.test', c.n), 'HASH' FROM c;

-- Órdenes: 80 % se concentran en el 5 % de las cafeterías (las "populares"); 2 % siguen en curso
CREATE TEMP TABLE a AS SELECT id_usuario AS id, row_number() OVER () AS n FROM usuario WHERE id_rol = 1;
CREATE UNIQUE INDEX ON a (n);
ANALYZE a;
CREATE TEMP TABLE o AS
SELECT gen_random_uuid() AS id, g,
       (SELECT c.id FROM c WHERE c.n = CASE WHEN g % 5 <> 0 THEN 1 + g % greatest(:cafeterias / 20, 1)
                                            ELSE 1 + g % :cafeterias END) AS cafe,
       now() - make_interval(mins => g % 260000) AS creado
  FROM generate_series(1, :ordenes) g;
CREATE INDEX ON o (g);
ANALYZE o;

INSERT INTO orden (id_orden, id_usuario, id_cafeteria, estado, recogida_en, codigo_recogida, creado_en)
SELECT o.id, a.id, o.cafe,
       CASE WHEN o.g % 50 = 0 THEN 'confirmada' WHEN o.g % 97 = 0 THEN 'cancelada' ELSE 'entregada' END::estado_orden,
       o.creado + interval '30 minutes', lpad((o.g % 1000000)::text, 6, '0'), o.creado
  FROM o JOIN a ON a.n = 1 + o.g % :alumnos;

INSERT INTO detalle_orden (id_orden, id_producto, cantidad, precio_unitario, creado_en)
SELECT o.id, p.id_producto, 1 + (o.g + p.k) % 3, p.precio, o.creado
  FROM o CROSS JOIN LATERAL (
    SELECT pr.id_producto, pr.precio, row_number() OVER () AS k
      FROM producto pr WHERE pr.id_cafeteria = o.cafe ORDER BY pr.nombre LIMIT 2) p;

INSERT INTO pago (id_orden, metodo, monto, estado, pagado_en, creado_en, actualizado_en)
SELECT d.id_orden, 'efectivo', sum(d.subtotal),
       CASE WHEN min(o.estado::text) = 'entregada' THEN 'pagado' WHEN min(o.estado::text) = 'cancelada' THEN 'cancelado' ELSE 'pendiente' END::estado_pago,
       CASE WHEN min(o.estado::text) = 'entregada' THEN min(o.creado_en) + interval '35 minutes' END,
       min(o.creado_en), min(o.creado_en) + interval '35 minutes'
  FROM detalle_orden d JOIN orden o ON o.id_orden = d.id_orden
 GROUP BY d.id_orden;

-- Historial y bitácora con el volumen que dejarían los triggers (3 estados y ~4 renglones de bitácora por orden)
INSERT INTO historial_estado_orden (id_orden, estado, cambiado_en)
SELECT o.id_orden, e.estado, o.creado_en + e.k * interval '10 minutes'
  FROM orden o CROSS JOIN (VALUES (0, 'confirmada'::estado_orden), (1, 'en_preparacion'), (2, 'lista')) e(k, estado);

INSERT INTO bitacora (tabla, id_registro, operacion, datos_antes, datos_despues, cambiado_en)
SELECT 'orden', o.id_orden::text, x.op,
       CASE WHEN x.op = 'U' THEN jsonb_build_object('estado', 'confirmada') END,
       jsonb_build_object('estado', o.estado, 'id_cafeteria', o.id_cafeteria),
       o.creado_en + x.k * interval '10 minutes'
  FROM orden o CROSS JOIN (VALUES ('I', 0), ('U', 1), ('U', 2), ('U', 3)) x(op, k);

COMMIT;
VACUUM ANALYZE;

SELECT relname AS tabla, n_live_tup AS filas, pg_size_pretty(pg_total_relation_size(relid)) AS tamano
  FROM pg_stat_user_tables WHERE schemaname = 'public' ORDER BY n_live_tup DESC;
SELECT pg_size_pretty(pg_database_size(current_database())) AS tamano_base;
