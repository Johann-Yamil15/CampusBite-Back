-- =====================================================================
-- CampusBite · TEC-03 · Smoke test del flujo completo (requiere seed_dev.sql)
-- Todo corre dentro de una transacción que termina en ROLLBACK: no deja datos.
--   psql -v ON_ERROR_STOP=1 -f tests/smoke_test.sql campusbite
-- =====================================================================
BEGIN;

DO $$
DECLARE
  c_alumno UUID := '00000000-0000-0000-0000-0000000000a1';
  c_admin  UUID := '00000000-0000-0000-0000-0000000000a2';
  c_cafe   UUID := '00000000-0000-0000-0000-0000000000c1';
  c_p1     UUID := '00000000-0000-0000-0000-0000000000e1';
  c_p3     UUID := '00000000-0000-0000-0000-0000000000e3';
  v_hora   TIMESTAMPTZ;
  v_orden  UUID;
  v_orden2 UUID;
  v_codigo TEXT;
  v_total  NUMERIC;
  v_n      INT;
  v_ok     BOOLEAN;
BEGIN
  -- Mañana a las 13:00 hora de México
  v_hora := (date_trunc('day', now() AT TIME ZONE 'America/Mexico_City') + interval '1 day 13 hours')
            AT TIME ZONE 'America/Mexico_City';

  -- 1. Estructura -----------------------------------------------------
  SELECT count(*) INTO v_n FROM information_schema.tables
   WHERE table_schema='public' AND table_name IN
     ('rol','usuario','cafeteria','horario_cafeteria','categoria','producto',
      'orden','detalle_orden','pago','historial_estado_orden','suscripcion_push');
  ASSERT v_n = 11, format('Se esperaban 11 tablas, hay %s', v_n);
  SELECT count(*) INTO v_n FROM rol;
  ASSERT v_n = 3, 'El catálogo rol debe tener 3 filas';
  RAISE NOTICE 'OK 1: 11 tablas y catálogo de roles';

  -- 2. Registro de alumno + duplicado --------------------------------
  PERFORM sp_registrar_usuario('Nuevo Alumno', 'Nuevo@Ejemplo.test', 'HASH_X', 'UTT9999999', '7719999999');
  v_ok := false;
  BEGIN
    PERFORM sp_registrar_usuario('Otro', 'nuevo@ejemplo.TEST', 'HASH_Y');   -- CITEXT: mismo correo
  EXCEPTION WHEN unique_violation THEN v_ok := true; END;
  ASSERT v_ok, 'El correo duplicado (sin distinguir mayúsculas) debió rechazarse';
  RAISE NOTICE 'OK 2: registro y correo duplicado (CITEXT)';

  -- 3. Orden en efectivo: nace confirmada, total = 2×55 + 25 ----------
  v_orden := sp_crear_orden(c_alumno, c_cafe, v_hora, 'efectivo',
    jsonb_build_array(jsonb_build_object('id_producto', c_p1, 'cantidad', 2),
                      jsonb_build_object('id_producto', c_p3, 'cantidad', 1)), 'Sin cebolla');
  SELECT total, codigo_recogida INTO v_total, v_codigo FROM orden WHERE id_orden = v_orden;
  ASSERT v_total = 135.00, format('Total esperado 135.00, fue %s', v_total);
  ASSERT (SELECT estado FROM orden WHERE id_orden = v_orden) = 'confirmada', 'Efectivo debe nacer confirmada';
  ASSERT (SELECT sum(subtotal) FROM detalle_orden WHERE id_orden = v_orden) = 135.00, 'subtotal generado incorrecto';
  RAISE NOTICE 'OK 3: orden en efectivo, total % y código %', v_total, v_codigo;

  -- 4. Estado inválido (saltar a entregada) debe fallar ----------------
  v_ok := false;
  BEGIN
    UPDATE orden SET estado = 'entregada' WHERE id_orden = v_orden;
  EXCEPTION WHEN check_violation THEN v_ok := true; END;
  ASSERT v_ok, 'La transición confirmada -> entregada debió bloquearse';
  RAISE NOTICE 'OK 4: máquina de estados bloquea transiciones inválidas';

  -- 5. Permisos: un alumno no puede cambiar estados de la cafetería ----
  v_ok := false;
  BEGIN
    PERFORM sp_cambiar_estado_orden(v_orden, 'en_preparacion', c_alumno);
  EXCEPTION WHEN insufficient_privilege THEN v_ok := true; END;
  ASSERT v_ok, 'Un alumno no debe poder avanzar la orden';
  RAISE NOTICE 'OK 5: control de permisos en sp_cambiar_estado_orden';

  -- 6. Flujo de la cafetería y entrega con código ----------------------
  PERFORM sp_cambiar_estado_orden(v_orden, 'en_preparacion', c_admin);
  PERFORM sp_cambiar_estado_orden(v_orden, 'lista', c_admin);
  v_ok := false;
  BEGIN
    PERFORM sp_entregar_orden(v_orden, CASE WHEN v_codigo = '000000' THEN '111111' ELSE '000000' END, c_admin);
  EXCEPTION WHEN OTHERS THEN v_ok := true; END;
  ASSERT v_ok, 'Un código de recogida incorrecto debió rechazarse';
  PERFORM sp_entregar_orden(v_orden, v_codigo, c_admin);
  ASSERT (SELECT estado FROM orden WHERE id_orden = v_orden) = 'entregada', 'Debe quedar entregada';
  ASSERT (SELECT estado FROM pago WHERE id_orden = v_orden) = 'pagado', 'El efectivo se cobra al entregar';
  SELECT count(*) INTO v_n FROM historial_estado_orden WHERE id_orden = v_orden;
  ASSERT v_n = 4, format('Historial esperado: 4 estados, hay %s', v_n);
  RAISE NOTICE 'OK 6: en_preparacion -> lista -> entregada, efectivo cobrado, historial de % estados', v_n;

  -- 7. Orden con tarjeta: pendiente -> pago -> confirmada --------------
  v_orden2 := sp_crear_orden(c_alumno, c_cafe, v_hora, 'tarjeta',
    jsonb_build_array(jsonb_build_object('id_producto', c_p3, 'cantidad', 2)));
  ASSERT (SELECT estado FROM orden WHERE id_orden = v_orden2) = 'pendiente', 'Tarjeta debe nacer pendiente';
  PERFORM sp_confirmar_pago(v_orden2, 'REF-TEST-001');
  ASSERT (SELECT estado FROM orden WHERE id_orden = v_orden2) = 'confirmada', 'Pago confirma la orden';
  RAISE NOTICE 'OK 7: tarjeta pendiente -> confirmada con sp_confirmar_pago';

  -- 8. Cancelación del alumno reembolsa el pago pagado -----------------
  PERFORM sp_cancelar_orden(v_orden2, c_alumno);
  ASSERT (SELECT estado FROM pago WHERE id_orden = v_orden2) = 'reembolsado', 'Pago pagado debe pasar a reembolsado';
  RAISE NOTICE 'OK 8: cancelación -> pago reembolsado';

  -- 9. Validaciones de sp_crear_orden ---------------------------------
  v_ok := false;
  BEGIN
    PERFORM sp_crear_orden(c_alumno, c_cafe, now() + interval '1 minute', 'efectivo',
      jsonb_build_array(jsonb_build_object('id_producto', c_p1, 'cantidad', 1)));
  EXCEPTION WHEN OTHERS THEN v_ok := true; END;
  ASSERT v_ok, 'Hora de recogida con menos de 5 min debió rechazarse';
  v_ok := false;
  BEGIN
    PERFORM sp_crear_orden(c_alumno, c_cafe, v_hora, 'efectivo',
      jsonb_build_array(jsonb_build_object('id_producto', c_p1, 'cantidad', 21)));
  EXCEPTION WHEN OTHERS THEN v_ok := true; END;
  ASSERT v_ok, 'Cantidad 21 debió rechazarse';
  RAISE NOTICE 'OK 9: validaciones de hora y cantidad';

  -- 10. Vistas --------------------------------------------------------
  SELECT count(*) INTO v_n FROM vw_menu_cafeteria;
  ASSERT v_n = 3, format('El menú debe tener 3 productos, hay %s', v_n);
  SELECT count(*) INTO v_n FROM vw_mis_ordenes WHERE id_usuario = c_alumno;
  ASSERT v_n = 2, format('El alumno debe tener 2 órdenes, tiene %s', v_n);
  RAISE NOTICE 'OK 10: vistas vw_menu_cafeteria y vw_mis_ordenes';
END $$;

-- 11. Mínimo privilegio: app_backend no toca tablas, solo funciones/vistas
DO $$
DECLARE v_ok BOOLEAN := false;
BEGIN
  SET LOCAL ROLE app_backend;
  BEGIN
    PERFORM 1 FROM usuario LIMIT 1;
  EXCEPTION WHEN insufficient_privilege THEN v_ok := true; END;
  ASSERT v_ok, 'app_backend NO debe poder leer la tabla usuario';
  PERFORM 1 FROM vw_menu_cafeteria LIMIT 1;           -- sí puede leer la vista
  RESET ROLE;
  RAISE NOTICE 'OK 11: app_backend sin acceso directo a tablas, con acceso a vistas';
END $$;

-- 12-16. Seguridad (V004) e índices (V005) -----------------------------------
DO $$
DECLARE
  c_alumno UUID := '00000000-0000-0000-0000-0000000000a1';
  c_admin  UUID := '00000000-0000-0000-0000-0000000000a2';
  c_cafe   UUID := '00000000-0000-0000-0000-0000000000c1';
  c_p1     UUID := '00000000-0000-0000-0000-0000000000e1';
  v_hora   TIMESTAMPTZ := (date_trunc('day', now() AT TIME ZONE 'America/Mexico_City') + interval '1 day 15 hours')
                          AT TIME ZONE 'America/Mexico_City';
  v_orden  UUID;
  v_n      INT;
  v_ok     BOOLEAN;
BEGIN
  -- 12. Límites del rol de la API
  ASSERT (SELECT rolconnlimit FROM pg_roles WHERE rolname = 'app_backend') = 50, 'app_backend debe tener límite de conexiones';
  ASSERT (SELECT bool_or(c LIKE 'statement_timeout=%') FROM pg_roles r, unnest(r.rolconfig) c WHERE r.rolname = 'app_backend'),
         'app_backend debe tener statement_timeout';
  RAISE NOTICE 'OK 12: límites de conexión y tiempo para app_backend';

  -- 13. La API lee con autorización: sin acceso a las vistas por persona
  SET LOCAL ROLE app_backend;
  v_orden := sp_crear_orden(c_alumno, c_cafe, v_hora, 'efectivo',
               jsonb_build_array(jsonb_build_object('id_producto', c_p1, 'cantidad', 1)));
  v_ok := false;
  BEGIN PERFORM 1 FROM vw_mis_ordenes LIMIT 1;
  EXCEPTION WHEN insufficient_privilege THEN v_ok := true; END;
  ASSERT v_ok, 'app_backend no debe leer vw_mis_ordenes directamente';
  v_ok := false;
  BEGIN PERFORM 1 FROM vw_cola_cafeteria LIMIT 1;
  EXCEPTION WHEN insufficient_privilege THEN v_ok := true; END;
  ASSERT v_ok, 'app_backend no debe leer vw_cola_cafeteria directamente';
  SELECT count(*) INTO v_n FROM sp_mis_ordenes(c_alumno);
  ASSERT v_n >= 1, 'sp_mis_ordenes debe devolver las órdenes del alumno';
  ASSERT NOT EXISTS (SELECT 1 FROM sp_mis_ordenes(c_alumno) WHERE id_usuario <> c_alumno), 'sp_mis_ordenes no debe mezclar usuarios';
  ASSERT (SELECT count(*) FROM sp_mis_ordenes(c_admin)) = 0, 'otro usuario no ve órdenes ajenas';
  RESET ROLE;
  RAISE NOTICE 'OK 13: vistas por persona cerradas; sp_mis_ordenes filtra por dueño';

  -- 14. Cola de la cafetería: solo su administrador (o el admin del sistema)
  SET LOCAL ROLE app_backend;
  v_ok := false;
  BEGIN PERFORM * FROM sp_cola_cafeteria(c_cafe, c_alumno);
  EXCEPTION WHEN insufficient_privilege THEN v_ok := true; END;
  ASSERT v_ok, 'Un alumno no debe ver la cola de la cafetería';
  SELECT count(*) INTO v_n FROM sp_cola_cafeteria(c_cafe, c_admin);
  ASSERT v_n >= 1, 'El encargado debe ver la cola con la orden nueva';
  RESET ROLE;
  RAISE NOTICE 'OK 14: sp_cola_cafeteria autoriza dentro de la base de datos';

  -- 15. Un rol cualquiera no puede ejecutar procedimientos, leer vistas ni crear objetos
  CREATE ROLE intruso NOLOGIN;
  SET LOCAL ROLE intruso;
  v_ok := false;
  BEGIN PERFORM sp_registrar_usuario('X Y', 'x@y.test', 'h');
  EXCEPTION WHEN insufficient_privilege THEN v_ok := true; END;
  ASSERT v_ok, 'PUBLIC no debe ejecutar procedimientos';
  v_ok := false;
  BEGIN PERFORM 1 FROM vw_menu_cafeteria LIMIT 1;
  EXCEPTION WHEN insufficient_privilege THEN v_ok := true; END;
  ASSERT v_ok, 'PUBLIC no debe leer vistas';
  v_ok := false;
  BEGIN EXECUTE 'CREATE TABLE public.trampa (id int)';
  EXCEPTION WHEN insufficient_privilege THEN v_ok := true; END;
  ASSERT v_ok, 'PUBLIC no debe crear objetos en el esquema public';
  RESET ROLE;
  RAISE NOTICE 'OK 15: un rol ajeno no ejecuta, lee ni crea nada';

  -- 16. Índices: sobrantes eliminados, útiles presentes
  ASSERT NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND indexname IN
    ('idx_horario_cafeteria_dia','idx_usuario_rol','idx_cafeteria_activa','idx_cafeteria_admin','idx_producto_categoria')),
    'Los índices depurados no deben existir';
  ASSERT (SELECT count(*) FROM pg_indexes WHERE schemaname = 'public' AND indexname IN
    ('idx_orden_usuario_fecha','idx_orden_cola_cafeteria','idx_producto_cafeteria','idx_historial_orden_fecha',
     'idx_detalle_producto','idx_push_usuario')) = 6, 'Faltan índices que sí se usan';
  RAISE NOTICE 'OK 16: índices depurados y los 6 útiles presentes';
END $$;

ROLLBACK;
\echo '>>> SMOKE TEST COMPLETO: todo OK (transacción revertida)'
