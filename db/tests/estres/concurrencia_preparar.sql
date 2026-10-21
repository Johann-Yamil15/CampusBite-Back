-- =====================================================================
-- JY-Sprint3 20/10/2026: preparación de la prueba de concurrencia (la usa concurrencia.sh).
-- SOLO en una base de pruebas desechable: crea tablas y procedimientos estres_* que no existen en el sistema.
-- Los procedimientos estres_* solo traducen números de pgbench (1..5) a ids y anotan el resultado; la lógica
-- que se prueba es la de los sp_* reales.
-- =====================================================================
\set ON_ERROR_STOP on

-- 5 cafeterías "populares" abiertas todo el día, con 2 productos, y 200 alumnos
CREATE TABLE estres_cafes (n INT PRIMARY KEY, id UUID NOT NULL, items JSONB NOT NULL);
CREATE TABLE estres_alumnos (n INT PRIMARY KEY, id UUID NOT NULL);
CREATE UNLOGGED TABLE estres_log (id BIGINT GENERATED ALWAYS AS IDENTITY, resultado TEXT NOT NULL, en TIMESTAMPTZ DEFAULT clock_timestamp());

DO $$
DECLARE
  v_cafe UUID;
  v_cat  UUID;
BEGIN
  FOR i IN 1..5 LOOP
    INSERT INTO cafeteria (nombre, ubicacion, es_del_campus, punto_entrega)
    VALUES (format('Concurrida %s', i), 'Edificio C', true, 'Ventanilla') RETURNING id_cafeteria INTO v_cafe;
    INSERT INTO cafeteria_metodo_pago (id_cafeteria, metodo) VALUES (v_cafe, 'efectivo');
    INSERT INTO horario_cafeteria (id_cafeteria, dia_semana, hora_apertura, hora_cierre)
    SELECT v_cafe, d, '00:00', '23:59:59' FROM generate_series(1, 7) d;
    INSERT INTO categoria (id_cafeteria, nombre) VALUES (v_cafe, 'General') RETURNING id_categoria INTO v_cat;
    INSERT INTO producto (id_cafeteria, id_categoria, nombre, precio)
    VALUES (v_cafe, v_cat, 'Torta', 45), (v_cafe, v_cat, 'Agua', 15);
    INSERT INTO estres_cafes (n, id, items)
    SELECT i, v_cafe, jsonb_agg(jsonb_build_object('id_producto', p.id_producto, 'cantidad', 1))
      FROM producto p WHERE p.id_cafeteria = v_cafe;
  END LOOP;

  INSERT INTO usuario (id_rol, nombre, correo, contrasena_hash)
  SELECT 1, format('Alumno Concurrente %s', g), format('concurrente%s@estres.test', g), 'HASH' FROM generate_series(1, 200) g;
  INSERT INTO estres_alumnos (n, id)
  SELECT row_number() OVER (ORDER BY u.correo), u.id_usuario FROM usuario u WHERE u.correo LIKE 'concurrente%@estres.test';
END $$;

-- Un alumno ordena en una cafetería popular. Errores esperados: la cafetería está inactiva.
-- Cualquier otro error se anota como "error" (la prueba falla si aparece alguno).
CREATE PROCEDURE estres_ordenar(p_cafe INT, p_alumno INT)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_cafe UUID; v_items JSONB; v_alumno UUID; v_id UUID;
BEGIN
  SELECT c.id, c.items INTO v_cafe, v_items FROM estres_cafes c WHERE c.n = p_cafe;
  SELECT a.id INTO v_alumno FROM estres_alumnos a WHERE a.n = p_alumno;
  CALL sp_crear_orden(v_alumno, v_cafe, now() + interval '30 minutes', 'efectivo', v_items, NULL, v_id);
  INSERT INTO estres_log (resultado) VALUES ('orden creada');
EXCEPTION
  WHEN raise_exception THEN
    INSERT INTO estres_log (resultado)
    VALUES (CASE WHEN SQLERRM = 'La cafetería no existe o no está activa' THEN 'orden rechazada: cafetería inactiva'
                 ELSE 'ERROR orden: ' || SQLERRM END);
  WHEN OTHERS THEN
    INSERT INTO estres_log (resultado) VALUES ('ERROR orden ' || SQLSTATE || ': ' || SQLERRM);
END $$;

-- Ediciones simultáneas de la misma cafetería con dos juegos de métodos de pago distintos
CREATE PROCEDURE estres_editar(p_juego INT)
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_cafe UUID;
BEGIN
  SELECT c.id INTO v_cafe FROM estres_cafes c WHERE c.n = 1;
  CALL sp_actualizar_cafeteria(v_cafe, '00000000-0000-0000-0000-0000000000a3', 'Concurrida 1', 'Edificio C', true,
       format('Ventanilla %s', p_juego),
       CASE WHEN p_juego = 1 THEN ARRAY['efectivo','tarjeta']::metodo_pago[] ELSE ARRAY['transferencia']::metodo_pago[] END);
  INSERT INTO estres_log (resultado) VALUES ('edición aplicada');
EXCEPTION WHEN OTHERS THEN
  INSERT INTO estres_log (resultado) VALUES ('ERROR edición ' || SQLSTATE || ': ' || SQLERRM);
END $$;

-- Muchos admins crean al mismo tiempo una cafetería con el mismo nombre: solo una debe existir
CREATE PROCEDURE estres_crear_mismo_nombre()
LANGUAGE plpgsql SECURITY DEFINER SET search_path = public, pg_temp AS $$
DECLARE
  v_id UUID;
BEGIN
  CALL sp_crear_cafeteria('00000000-0000-0000-0000-0000000000a3', 'Carrera de nombres', 'Edificio R', true, 'Puerta',
       ARRAY['efectivo']::metodo_pago[], v_id);
  INSERT INTO estres_log (resultado) VALUES ('alta con nombre repetido: creada');
EXCEPTION
  WHEN unique_violation THEN INSERT INTO estres_log (resultado) VALUES ('alta con nombre repetido: rechazada (409)');
  WHEN OTHERS THEN INSERT INTO estres_log (resultado) VALUES ('ERROR alta ' || SQLSTATE || ': ' || SQLERRM);
END $$;

-- El admin cancela las órdenes en curso de una cafetería (una transacción)
CREATE PROCEDURE estres_cancelar_en_curso(p_cafe INT)
LANGUAGE plpgsql AS $$
DECLARE
  v_cafe UUID; r RECORD;
BEGIN
  SELECT c.id INTO v_cafe FROM estres_cafes c WHERE c.n = p_cafe;
  FOR r IN SELECT o.id_orden FROM orden o
            WHERE o.id_cafeteria = v_cafe AND o.estado IN ('pendiente','confirmada','en_preparacion','lista') LOOP
    BEGIN
      CALL sp_cambiar_estado_orden(r.id_orden, 'cancelada', '00000000-0000-0000-0000-0000000000a3');
    EXCEPTION WHEN OTHERS THEN NULL;   -- otra sesión ya la cambió
    END;
  END LOOP;
END $$;

-- Un intento de baja: p_ok = false si la cafetería tenía órdenes en curso (409)
CREATE PROCEDURE estres_intentar_baja(p_cafe INT, INOUT p_ok BOOLEAN)
LANGUAGE plpgsql AS $$
DECLARE
  v_cafe UUID;
BEGIN
  SELECT c.id INTO v_cafe FROM estres_cafes c WHERE c.n = p_cafe;
  CALL sp_cambiar_activa_cafeteria(v_cafe, false, '00000000-0000-0000-0000-0000000000a3');
  p_ok := true;
EXCEPTION WHEN object_in_use THEN p_ok := false;
END $$;

-- Ciclos de baja y reactivación mientras llegan órdenes. Después de cada baja exitosa revisa la regla:
-- una cafetería inactiva NO puede tener órdenes en curso. Si tiene, una orden se coló durante la baja.
-- Cada paso es su propia transacción (COMMIT), igual que llamadas separadas de la API.
CREATE PROCEDURE estres_ciclos_de_baja(p_cafe INT, p_ciclos INT)
LANGUAGE plpgsql AS $$
DECLARE
  v_cafe UUID; v_ok BOOLEAN; v_intentos INT; v_coladas INT;
BEGIN
  SELECT c.id INTO v_cafe FROM estres_cafes c WHERE c.n = p_cafe;
  FOR i IN 1..p_ciclos LOOP
    v_ok := false; v_intentos := 0;
    WHILE NOT v_ok AND v_intentos < 500 LOOP
      CALL estres_cancelar_en_curso(p_cafe);
      COMMIT;
      CALL estres_intentar_baja(p_cafe, v_ok);
      COMMIT;
      v_intentos := v_intentos + 1;
    END LOOP;
    IF NOT v_ok THEN
      INSERT INTO estres_log (resultado) VALUES ('baja no lograda en 500 intentos');
      COMMIT;
      CONTINUE;
    END IF;
    INSERT INTO estres_log (resultado) VALUES ('baja exitosa'), ('intentos de baja: ' || v_intentos);
    COMMIT;

    PERFORM pg_sleep(0.05);                       -- deja terminar cualquier orden que hubiera empezado
    SELECT count(*) INTO v_coladas FROM orden o
     WHERE o.id_cafeteria = v_cafe AND o.estado IN ('pendiente','confirmada','en_preparacion','lista');
    IF v_coladas > 0 THEN
      INSERT INTO estres_log (resultado) VALUES ('VIOLACIÓN: ' || v_coladas || ' orden(es) en curso en una cafetería inactiva');
    END IF;
    COMMIT;

    CALL estres_cancelar_en_curso(p_cafe);
    COMMIT;
    CALL sp_cambiar_activa_cafeteria(v_cafe, true, '00000000-0000-0000-0000-0000000000a3');
    COMMIT;
    PERFORM pg_sleep(0.02);
  END LOOP;
END $$;

GRANT EXECUTE ON PROCEDURE estres_ordenar, estres_editar, estres_crear_mismo_nombre TO app_backend;
