-- =====================================================================
-- CampusBite · Smoke test del flujo completo (requiere los datos de 90_datos_dev)
-- JY-Sprint2 09/10/2026: procedimientos con CALL, métodos de pago en tabla, sin orden.total, fechas
-- revisadas y verificaciones de redundancia, RLS y permisos.
-- Todo corre dentro de una transacción que termina en ROLLBACK: no deja datos.
--   psql -U postgres -d campusbite -v ON_ERROR_STOP=1 -f db/tests/smoke_test.sql
-- =====================================================================
BEGIN;

-- 1-12. Estructura y flujo de negocio -------------------------------------------
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
  v_id     UUID;
  v_rol    SMALLINT;
  v_hash   VARCHAR;
  v_activo BOOLEAN;
  v_codigo TEXT;
  v_json   JSONB;
  v_n      INT;
  v_ok     BOOLEAN;
  v_previas INT;   -- órdenes que el alumno ya tenía (una base actualizada puede traer datos)
BEGIN
  SELECT count(*) INTO v_previas FROM orden o WHERE o.id_usuario = c_alumno;
  -- Mañana a las 13:00 hora de México
  v_hora := (date_trunc('day', now() AT TIME ZONE 'America/Mexico_City') + interval '1 day 13 hours')
            AT TIME ZONE 'America/Mexico_City';

  -- 1. Estructura: 12 tablas, sin las columnas redundantes de V1, sp_* son PROCEDURE --------
  SELECT count(*) INTO v_n FROM information_schema.tables
   WHERE table_schema = 'public' AND table_type = 'BASE TABLE' AND table_name IN
     ('rol','usuario','cafeteria','cafeteria_metodo_pago','horario_cafeteria','categoria','producto',
      'orden','detalle_orden','pago','historial_estado_orden','suscripcion_push','bitacora');
  ASSERT v_n = 13, format('Se esperaban 13 tablas, hay %s', v_n);
  SELECT count(*) INTO v_n FROM rol;
  ASSERT v_n = 3, 'El catálogo rol debe tener 3 filas';
  SELECT count(*) INTO v_n FROM information_schema.columns
   WHERE table_schema = 'public' AND (table_name, column_name) IN
     (('cafeteria','id_admin'), ('cafeteria','acepta_efectivo'), ('cafeteria','acepta_tarjeta'),
      ('cafeteria','acepta_transferencia'), ('orden','total'), ('orden','actualizado_en'),
      ('orden','creada_en'), ('orden','hora_recogida'), ('suscripcion_push','creada_en'));
  ASSERT v_n = 0, format('Quedan %s columnas de V1 que debían quitarse o renombrarse', v_n);
  SELECT count(*) INTO v_n FROM pg_proc p
   WHERE p.pronamespace = 'public'::regnamespace AND p.proname LIKE 'sp\_%' AND p.prokind <> 'p';
  ASSERT v_n = 0, format('Hay %s sp_* que no son PROCEDURE', v_n);
  SELECT count(*) INTO v_n FROM pg_proc p
   WHERE p.pronamespace = 'public'::regnamespace AND p.proname LIKE 'sp\_%' AND p.prokind = 'p';
  ASSERT v_n = 12, format('Se esperaban 12 procedimientos sp_*, hay %s', v_n);
  RAISE NOTICE 'OK 1: 13 tablas, columnas de V1 retiradas, 12 sp_* y todos son PROCEDURE';

  -- 2. Registro + duplicado + credenciales -------------------------------------------
  CALL sp_registrar_usuario('Nuevo Alumno', 'Nuevo@Ejemplo.test', 'HASH_X', 'UTT9999999', '7719999999', v_id);
  ASSERT v_id IS NOT NULL, 'sp_registrar_usuario debe devolver el id';
  v_ok := false;
  BEGIN
    CALL sp_registrar_usuario('Otro', 'nuevo@ejemplo.TEST', 'HASH_Y', NULL, NULL, v_id);   -- CITEXT: mismo correo
  EXCEPTION WHEN unique_violation THEN v_ok := true; END;
  ASSERT v_ok, 'El correo duplicado (sin distinguir mayúsculas) debió rechazarse';
  CALL sp_obtener_credenciales('NUEVO@ejemplo.test', v_id, v_rol, v_hash, v_activo);
  ASSERT v_rol = 1 AND v_hash = 'HASH_X' AND v_activo, 'sp_obtener_credenciales debe encontrar al alumno';
  CALL sp_obtener_credenciales('nadie@ejemplo.test', v_id, v_rol, v_hash, v_activo);
  ASSERT v_id IS NULL, 'Un correo inexistente debe devolver NULL';
  RAISE NOTICE 'OK 2: registro, correo duplicado (CITEXT) y credenciales';

  -- 3. Orden en efectivo: nace confirmada, monto = 2×55 + 25 ------------------------
  CALL sp_crear_orden(c_alumno, c_cafe, v_hora, 'efectivo',
    jsonb_build_array(jsonb_build_object('id_producto', c_p1, 'cantidad', 2),
                      jsonb_build_object('id_producto', c_p3, 'cantidad', 1)), 'Sin cebolla', v_orden);
  SELECT o.codigo_recogida INTO v_codigo FROM orden o WHERE o.id_orden = v_orden;
  ASSERT (SELECT p.monto FROM pago p WHERE p.id_orden = v_orden) = 135.00, 'pago.monto debe ser 135.00';
  ASSERT (SELECT o.estado FROM orden o WHERE o.id_orden = v_orden) = 'confirmada', 'Efectivo debe nacer confirmada';
  ASSERT (SELECT sum(d.subtotal) FROM detalle_orden d WHERE d.id_orden = v_orden) = 135.00, 'subtotal generado incorrecto';
  SET CONSTRAINTS ALL IMMEDIATE;     -- revisa ya la regla diferida monto = detalle
  SET CONSTRAINTS ALL DEFERRED;
  RAISE NOTICE 'OK 3: orden en efectivo, monto 135.00 = suma del detalle, código %', v_codigo;

  -- 4. Estado inválido (saltar a entregada) debe fallar ----------------------------
  v_ok := false;
  BEGIN
    UPDATE orden SET estado = 'entregada' WHERE id_orden = v_orden;
  EXCEPTION WHEN check_violation THEN v_ok := true; END;
  ASSERT v_ok, 'La transición confirmada -> entregada debió bloquearse';
  RAISE NOTICE 'OK 4: máquina de estados bloquea transiciones inválidas';

  -- 5. Permisos: un alumno no puede cambiar estados de la cafetería ----------------
  v_ok := false;
  BEGIN
    CALL sp_cambiar_estado_orden(v_orden, 'en_preparacion', c_alumno);
  EXCEPTION WHEN insufficient_privilege THEN v_ok := true; END;
  ASSERT v_ok, 'Un alumno no debe poder avanzar la orden';
  RAISE NOTICE 'OK 5: control de permisos en sp_cambiar_estado_orden';

  -- 6. Flujo de la cafetería y entrega con código ----------------------------------
  CALL sp_cambiar_estado_orden(v_orden, 'en_preparacion', c_admin);
  CALL sp_cambiar_estado_orden(v_orden, 'lista', c_admin);
  v_ok := false;
  BEGIN
    CALL sp_entregar_orden(v_orden, CASE WHEN v_codigo = '000000' THEN '111111' ELSE '000000' END, c_admin);
  EXCEPTION WHEN OTHERS THEN v_ok := true; END;
  ASSERT v_ok, 'Un código de recogida incorrecto debió rechazarse';
  CALL sp_entregar_orden(v_orden, v_codigo, c_admin);
  ASSERT (SELECT o.estado FROM orden o WHERE o.id_orden = v_orden) = 'entregada', 'Debe quedar entregada';
  ASSERT (SELECT p.estado FROM pago p WHERE p.id_orden = v_orden) = 'pagado', 'El efectivo se cobra al entregar';
  SELECT count(*) INTO v_n FROM historial_estado_orden h WHERE h.id_orden = v_orden;
  ASSERT v_n = 4, format('Historial esperado: 4 estados, hay %s', v_n);
  RAISE NOTICE 'OK 6: en_preparacion -> lista -> entregada, efectivo cobrado, historial de % estados', v_n;

  -- 7. Orden con tarjeta: pendiente -> pago -> confirmada --------------------------
  CALL sp_crear_orden(c_alumno, c_cafe, v_hora, 'tarjeta',
    jsonb_build_array(jsonb_build_object('id_producto', c_p3, 'cantidad', 2)), NULL, v_orden2);
  ASSERT (SELECT o.estado FROM orden o WHERE o.id_orden = v_orden2) = 'pendiente', 'Tarjeta debe nacer pendiente';
  CALL sp_confirmar_pago(v_orden2, 'REF-TEST-001');
  ASSERT (SELECT o.estado FROM orden o WHERE o.id_orden = v_orden2) = 'confirmada', 'Pago confirma la orden';
  RAISE NOTICE 'OK 7: tarjeta pendiente -> confirmada con sp_confirmar_pago';

  -- 8. Cancelación del alumno reembolsa el pago pagado -----------------------------
  CALL sp_cancelar_orden(v_orden2, c_alumno);
  ASSERT (SELECT p.estado FROM pago p WHERE p.id_orden = v_orden2) = 'reembolsado', 'Pago pagado debe pasar a reembolsado';
  RAISE NOTICE 'OK 8: cancelación -> pago reembolsado';

  -- 9. Validaciones de sp_crear_orden: hora, cantidad, items y método de pago -------
  v_ok := false;
  BEGIN
    CALL sp_crear_orden(c_alumno, c_cafe, now() + interval '1 minute', 'efectivo',
      jsonb_build_array(jsonb_build_object('id_producto', c_p1, 'cantidad', 1)), NULL, v_id);
  EXCEPTION WHEN OTHERS THEN v_ok := true; END;
  ASSERT v_ok, 'Hora de recogida con menos de 5 min debió rechazarse';
  v_ok := false;
  BEGIN
    CALL sp_crear_orden(c_alumno, c_cafe, v_hora, 'efectivo',
      jsonb_build_array(jsonb_build_object('id_producto', c_p1, 'cantidad', 21)), NULL, v_id);
  EXCEPTION WHEN OTHERS THEN v_ok := true; END;
  ASSERT v_ok, 'Cantidad 21 debió rechazarse';
  v_ok := false;
  BEGIN
    CALL sp_crear_orden(c_alumno, c_cafe, v_hora, 'efectivo', '{"no":"es arreglo"}'::jsonb, NULL, v_id);
  EXCEPTION WHEN raise_exception THEN v_ok := SQLERRM LIKE 'La orden debe incluir%'; END;
  ASSERT v_ok, 'Un items que no es arreglo debe dar el mensaje de negocio, no un error interno';
  DELETE FROM cafeteria_metodo_pago WHERE id_cafeteria = c_cafe AND metodo = 'transferencia';
  v_ok := false;
  BEGIN
    CALL sp_crear_orden(c_alumno, c_cafe, v_hora, 'transferencia',
      jsonb_build_array(jsonb_build_object('id_producto', c_p1, 'cantidad', 1)), NULL, v_id);
  EXCEPTION WHEN OTHERS THEN v_ok := SQLERRM LIKE '%no acepta el método de pago%'; END;
  ASSERT v_ok, 'Un método que la cafetería no acepta debió rechazarse';
  INSERT INTO cafeteria_metodo_pago (id_cafeteria, metodo) VALUES (c_cafe, 'transferencia');
  RAISE NOTICE 'OK 9: validaciones de hora, cantidad, items y método de pago (cafeteria_metodo_pago)';

  -- 10. Lecturas por procedimiento -----------------------------------------------
  CALL sp_menu_cafeteria(NULL, v_json);
  ASSERT jsonb_array_length(v_json) = 1, 'El menú debe traer 1 cafetería';
  ASSERT jsonb_array_length(v_json->0->'productos') = 3, 'El menú debe traer 3 productos';
  ASSERT jsonb_array_length(v_json->0->'metodos_pago') = 3, 'La cafetería debe aceptar 3 métodos';
  CALL sp_mis_ordenes(c_alumno, 100, 0, v_json);
  ASSERT jsonb_array_length(v_json) = v_previas + 2,
         format('El alumno debe tener %s órdenes, tiene %s', v_previas + 2, jsonb_array_length(v_json));
  ASSERT (SELECT (e->>'total')::numeric FROM jsonb_array_elements(v_json) e
           WHERE (e->>'id_orden')::uuid = v_orden) = 135.00, 'El total de la orden sale de pago.monto';
  CALL sp_mis_ordenes(c_alumno, 1, 0, v_json);
  ASSERT jsonb_array_length(v_json) = 1, 'sp_mis_ordenes debe respetar el límite';
  RAISE NOTICE 'OK 10: sp_menu_cafeteria y sp_mis_ordenes devuelven JSON (total desde pago.monto)';

  -- 11. Círculo usuario-cafeteria-orden y copia orden.id_cafeteria -------------------
  v_ok := false;
  BEGIN
    UPDATE usuario SET id_cafeteria = c_cafe WHERE id_usuario = c_alumno;
  EXCEPTION WHEN check_violation THEN v_ok := true; END;
  ASSERT v_ok, 'Un alumno no puede quedar asignado a una cafetería';
  v_ok := false;
  BEGIN
    UPDATE orden SET id_cafeteria = gen_random_uuid() WHERE id_orden = v_orden;
  EXCEPTION WHEN check_violation THEN v_ok := true; END;
  ASSERT v_ok, 'La cafetería de una orden no se puede cambiar';
  v_ok := false;
  BEGIN
    UPDATE producto SET id_cafeteria = gen_random_uuid() WHERE id_producto = c_p1;
  EXCEPTION WHEN check_violation THEN v_ok := true; END;
  ASSERT v_ok, 'La cafetería de un producto no se puede cambiar';
  v_ok := false;
  BEGIN
    UPDATE pago SET monto = monto + 1 WHERE id_orden = v_orden;
    SET CONSTRAINTS ALL IMMEDIATE;
  EXCEPTION WHEN check_violation THEN v_ok := true; END;
  SET CONSTRAINTS ALL DEFERRED;
  ASSERT v_ok, 'pago.monto distinto de la suma del detalle debió rechazarse';
  RAISE NOTICE 'OK 11: encargado solo rol 2, cafetería fija en orden y producto, monto = detalle';

  -- 12. Fechas ----------------------------------------------------------------------
  v_ok := false;
  BEGIN
    UPDATE orden SET recogida_en = creado_en - interval '1 minute' WHERE id_orden = v_orden;
  EXCEPTION WHEN check_violation THEN v_ok := true; END;
  ASSERT v_ok, 'recogida_en anterior a creado_en debió rechazarse';
  v_ok := false;
  BEGIN
    INSERT INTO usuario (id_rol, nombre, correo, contrasena_hash, creado_en, actualizado_en)
    VALUES (1, 'Fecha Mala', 'fecha@ejemplo.test', 'h', now(), now() - interval '1 day');
  EXCEPTION WHEN check_violation THEN v_ok := true; END;
  ASSERT v_ok, 'actualizado_en anterior a creado_en debió rechazarse';
  SELECT count(*) INTO v_n FROM information_schema.columns
   WHERE table_schema = 'public' AND column_name LIKE '%\_en' AND data_type <> 'timestamp with time zone';
  ASSERT v_n = 0, 'Toda columna *_en debe ser TIMESTAMPTZ';
  RAISE NOTICE 'OK 12: CHECK de fechas y convención *_en = TIMESTAMPTZ';
END $$;

-- 13-19. Seguridad, índices y RLS ------------------------------------------------
DO $$
DECLARE
  c_alumno UUID := '00000000-0000-0000-0000-0000000000a1';
  c_admin  UUID := '00000000-0000-0000-0000-0000000000a2';
  c_cafe   UUID := '00000000-0000-0000-0000-0000000000c1';
  c_p1     UUID := '00000000-0000-0000-0000-0000000000e1';
  v_hora   TIMESTAMPTZ := (date_trunc('day', now() AT TIME ZONE 'America/Mexico_City') + interval '1 day 15 hours')
                          AT TIME ZONE 'America/Mexico_City';
  v_orden  UUID;
  v_json   JSONB;
  v_n      INT;
  v_ok     BOOLEAN;
  v_sp     TEXT;
  v_oid    OID;
BEGIN
  -- 13. app_backend no lee tablas ni vistas; sí ejecuta procedimientos
  SET LOCAL ROLE app_backend;
  v_ok := false;
  BEGIN PERFORM 1 FROM usuario LIMIT 1;
  EXCEPTION WHEN insufficient_privilege THEN v_ok := true; END;
  ASSERT v_ok, 'app_backend NO debe poder leer la tabla usuario';
  FOREACH v_sp IN ARRAY ARRAY['vw_menu_cafeteria','vw_mis_ordenes','vw_cola_cafeteria','vw_metodos_pago_cafeteria'] LOOP
    v_ok := false;
    BEGIN EXECUTE format('SELECT 1 FROM %I LIMIT 1', v_sp);
    EXCEPTION WHEN insufficient_privilege THEN v_ok := true; END;
    ASSERT v_ok, format('app_backend no debe leer %s directamente', v_sp);
  END LOOP;
  CALL sp_crear_orden(c_alumno, c_cafe, v_hora, 'efectivo',
         jsonb_build_array(jsonb_build_object('id_producto', c_p1, 'cantidad', 1)), NULL, v_orden);
  -- Las reglas diferidas corren en el COMMIT como app_backend (no como el dueño del procedimiento):
  -- se fuerzan aquí porque esta prueba termina en ROLLBACK y nunca llega al COMMIT.
  SET CONSTRAINTS ALL IMMEDIATE;
  SET CONSTRAINTS ALL DEFERRED;
  CALL sp_menu_cafeteria(c_cafe, v_json);
  ASSERT jsonb_array_length(v_json) = 1, 'app_backend debe leer el menú por procedimiento';
  RESET ROLE;
  RAISE NOTICE 'OK 13: app_backend sin tablas ni vistas; usa CALL sp_*';

  -- 14. Límites del rol de la API
  ASSERT (SELECT r.rolconnlimit FROM pg_roles r WHERE r.rolname = 'app_backend') = 50, 'app_backend debe tener límite de conexiones';
  ASSERT (SELECT bool_or(c LIKE 'statement_timeout=%') FROM pg_roles r, unnest(r.rolconfig) c WHERE r.rolname = 'app_backend'),
         'app_backend debe tener statement_timeout';
  RAISE NOTICE 'OK 14: límites de conexión y tiempo para app_backend';

  -- 15. Lecturas con autorización dentro de la base
  SET LOCAL ROLE app_backend;
  CALL sp_mis_ordenes(c_alumno, 20, 0, v_json);
  ASSERT jsonb_array_length(v_json) >= 1, 'sp_mis_ordenes debe devolver las órdenes del alumno';
  CALL sp_mis_ordenes(c_admin, 20, 0, v_json);
  ASSERT jsonb_array_length(v_json) = 0, 'otro usuario no ve órdenes ajenas';
  v_ok := false;
  BEGIN CALL sp_cola_cafeteria(c_cafe, c_alumno, v_json);
  EXCEPTION WHEN insufficient_privilege THEN v_ok := true; END;
  ASSERT v_ok, 'Un alumno no debe ver la cola de la cafetería';
  CALL sp_cola_cafeteria(c_cafe, c_admin, v_json);
  ASSERT jsonb_array_length(v_json) >= 1, 'El encargado debe ver la cola con la orden nueva';
  v_ok := false;
  BEGIN CALL sp_exigir_gestor_cafeteria(c_cafe, c_admin, 'x');
  EXCEPTION WHEN insufficient_privilege THEN v_ok := true; END;
  ASSERT v_ok, 'sp_exigir_gestor_cafeteria es interno: app_backend no debe ejecutarlo';
  RESET ROLE;
  RAISE NOTICE 'OK 15: sp_mis_ordenes filtra por dueño, sp_cola_cafeteria autoriza, el sp interno está cerrado';

  -- 16. Un rol cualquiera no ejecuta NINGÚN procedimiento (antes sp_mis_ordenes y sp_cola_cafeteria sí podía)
  CREATE ROLE intruso NOLOGIN;
  FOR v_oid IN SELECT p.oid FROM pg_proc p
                WHERE p.pronamespace = 'public'::regnamespace AND p.proname LIKE 'sp\_%' LOOP
    ASSERT NOT has_function_privilege('intruso', v_oid, 'EXECUTE'), format('PUBLIC no debe ejecutar %s', v_oid::regproc);
  END LOOP;
  SET LOCAL ROLE intruso;
  v_ok := false;
  BEGIN CALL sp_mis_ordenes(c_alumno, 20, 0, v_json);
  EXCEPTION WHEN insufficient_privilege THEN v_ok := true; END;
  ASSERT v_ok, 'PUBLIC no debe ejecutar sp_mis_ordenes';
  v_ok := false;
  BEGIN EXECUTE 'CREATE TABLE public.trampa (id int)';
  EXCEPTION WHEN insufficient_privilege THEN v_ok := true; END;
  ASSERT v_ok, 'PUBLIC no debe crear objetos en el esquema public';
  RESET ROLE;
  RAISE NOTICE 'OK 16: un rol ajeno no ejecuta ningún sp_ ni crea objetos';

  -- 17. Índices: sobrantes eliminados, útiles presentes
  ASSERT NOT EXISTS (SELECT 1 FROM pg_indexes i WHERE i.schemaname = 'public' AND i.indexname IN
    ('idx_horario_cafeteria_dia','idx_usuario_rol','idx_cafeteria_activa','idx_cafeteria_admin','idx_producto_categoria')),
    'Los índices depurados no deben existir';
  ASSERT (SELECT count(*) FROM pg_indexes i WHERE i.schemaname = 'public' AND i.indexname IN
    ('idx_orden_usuario_fecha','idx_orden_cola_cafeteria','idx_producto_cafeteria','idx_historial_orden_fecha',
     'idx_detalle_producto','idx_push_usuario')) = 6, 'Faltan índices que sí se usan';
  RAISE NOTICE 'OK 17: índices depurados y los 6 útiles presentes';

  -- 18. RLS activo en todas las tablas
  SELECT count(*) INTO v_n FROM pg_class c
   WHERE c.relnamespace = 'public'::regnamespace AND c.relkind = 'r' AND NOT c.relrowsecurity
     AND c.relname NOT LIKE 'databasechangelog%';   -- tablas de control de Liquibase (local)
  ASSERT v_n = 0, format('%s tablas sin RLS', v_n);
  RAISE NOTICE 'OK 18: RLS activo en las 13 tablas';

  -- 19. Versión marcada en el esquema
  --     (la pone el script consolidado; con Liquibase el registro está en databasechangelog)
  IF to_regclass('public.databasechangelog') IS NULL THEN
    ASSERT obj_description('public'::regnamespace) LIKE 'CampusBite V%', 'El esquema debe tener la marca de versión';
    RAISE NOTICE 'OK 19: %', obj_description('public'::regnamespace);
  ELSE
    SELECT count(*) INTO v_n FROM databasechangelog;
    RAISE NOTICE 'OK 19: base de Liquibase con % changesets aplicados', v_n;
  END IF;
END $$;

-- 20. Fechas en todas las tablas y bitácora general -------------------------------
DO $$
DECLARE
  c_alumno UUID := '00000000-0000-0000-0000-0000000000a1';
  c_admin  UUID := '00000000-0000-0000-0000-0000000000a2';
  c_sistema UUID := '00000000-0000-0000-0000-0000000000a3';
  c_cafe   UUID := '00000000-0000-0000-0000-0000000000c1';
  c_p1     UUID := '00000000-0000-0000-0000-0000000000e1';
  v_hora   TIMESTAMPTZ := (date_trunc('day', now() AT TIME ZONE 'America/Mexico_City') + interval '1 day 16 hours')
                          AT TIME ZONE 'America/Mexico_City';
  v_orden  UUID;
  v_id     UUID;
  v_json   JSONB;
  v_n      INT;
  v_ok     BOOLEAN;
BEGIN
  -- Toda tabla tiene su fecha de creación (historial_estado_orden y bitacora: cambiado_en)
  SELECT count(*) INTO v_n FROM information_schema.tables t
   WHERE t.table_schema = 'public' AND t.table_type = 'BASE TABLE' AND t.table_name NOT LIKE 'databasechangelog%'
     AND NOT EXISTS (SELECT 1 FROM information_schema.columns c
                      WHERE c.table_schema = 'public' AND c.table_name = t.table_name
                        AND c.column_name IN ('creado_en', 'cambiado_en'));
  ASSERT v_n = 0, format('%s tablas sin fecha de creación', v_n);

  -- Alta, cambio y baja quedan en la bitácora, con autor y sin contraseña
  CALL sp_registrar_usuario('Auditado Uno', 'auditado@ejemplo.test', 'HASH_SECRETO', NULL, NULL, v_id);
  UPDATE usuario SET telefono = '7711112222' WHERE id_usuario = v_id;
  UPDATE usuario SET telefono = '7711112222' WHERE id_usuario = v_id;          -- sin cambio real: no se registra
  ASSERT (SELECT string_agg(b.operacion, '' ORDER BY b.id_bitacora) FROM bitacora b
           WHERE b.tabla = 'usuario' AND b.id_registro = v_id::text) = 'IU', 'La bitácora debe tener el alta y un cambio';
  ASSERT NOT EXISTS (SELECT 1 FROM bitacora b WHERE b.datos_despues ? 'contrasena_hash' OR b.datos_antes ? 'contrasena_hash'),
         'La bitácora nunca debe guardar contrasena_hash';

  CALL sp_crear_orden(c_alumno, c_cafe, v_hora, 'efectivo',
         jsonb_build_array(jsonb_build_object('id_producto', c_p1, 'cantidad', 1)), NULL, v_orden);
  CALL sp_cambiar_estado_orden(v_orden, 'en_preparacion', c_admin);
  ASSERT (SELECT b.cambiado_por FROM bitacora b WHERE b.tabla = 'orden' AND b.id_registro = v_orden::text
           AND b.operacion = 'U') = c_admin, 'La bitácora debe registrar quién cambió la orden';
  ASSERT (SELECT count(*) FROM bitacora b WHERE b.tabla IN ('detalle_orden','pago')
           AND (b.datos_despues->>'id_orden')::uuid = v_orden) = 2, 'El detalle y el pago de la orden deben estar en la bitácora';
  ASSERT (SELECT p.creado_en FROM pago p WHERE p.id_orden = v_orden) IS NOT NULL, 'pago debe tener creado_en';

  -- Consulta del historial: solo admin_sistema
  SET LOCAL ROLE app_backend;
  CALL sp_bitacora_registro(c_sistema, 'orden', v_orden::text, 50, v_json);
  ASSERT jsonb_array_length(v_json) = 2 AND v_json->0->>'operacion' = 'U', 'Historial de la orden: cambio y alta, el más reciente primero';
  v_ok := false;
  BEGIN CALL sp_bitacora_registro(c_admin, 'orden', v_orden::text, 50, v_json);
  EXCEPTION WHEN insufficient_privilege THEN v_ok := true; END;
  ASSERT v_ok, 'Solo admin_sistema puede ver la bitácora';
  v_ok := false;
  BEGIN PERFORM 1 FROM bitacora LIMIT 1;
  EXCEPTION WHEN insufficient_privilege THEN v_ok := true; END;
  ASSERT v_ok, 'app_backend no debe leer la bitácora directamente';
  RESET ROLE;
  RAISE NOTICE 'OK 20: fecha de creación en todas las tablas; bitácora con alta, cambio, autor y sin contraseñas';
END $$;

ROLLBACK;
\echo '>>> SMOKE TEST COMPLETO: 20 de 20 OK (transacción revertida)'
