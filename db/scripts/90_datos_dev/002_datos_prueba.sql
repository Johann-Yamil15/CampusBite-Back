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
