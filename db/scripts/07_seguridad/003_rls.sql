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
    'orden', 'detalle_orden', 'pago', 'historial_estado_orden', 'suscripcion_push', 'bitacora',
    -- JJ-Sprint5 13/11/2026
    'cierre_cafeteria', 'promocion', 'favorito_cafeteria', 'favorito_producto', 'preferencia_alumno', 'reporte_orden']
  LOOP
    EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY', v_tabla);
  END LOOP;
END $$;
