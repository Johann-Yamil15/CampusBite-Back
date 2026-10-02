-- Rollback de V002
DROP FUNCTION IF EXISTS sp_entregar_orden(UUID, VARCHAR, UUID);
DROP FUNCTION IF EXISTS sp_cancelar_orden(UUID, UUID);
DROP FUNCTION IF EXISTS sp_cambiar_estado_orden(UUID, estado_orden, UUID);
DROP FUNCTION IF EXISTS sp_confirmar_pago(UUID, VARCHAR);
DROP FUNCTION IF EXISTS sp_crear_orden(UUID, UUID, TIMESTAMPTZ, metodo_pago, JSONB, VARCHAR);
DROP FUNCTION IF EXISTS sp_obtener_credenciales(VARCHAR);
DROP FUNCTION IF EXISTS sp_registrar_usuario(VARCHAR, VARCHAR, VARCHAR, VARCHAR, VARCHAR);

DROP TRIGGER IF EXISTS trg_detalle_valida      ON detalle_orden;
DROP TRIGGER IF EXISTS trg_orden_cancelada     ON orden;
DROP TRIGGER IF EXISTS trg_orden_historial_upd ON orden;
DROP TRIGGER IF EXISTS trg_orden_historial_ins ON orden;
DROP TRIGGER IF EXISTS trg_orden_transicion    ON orden;
DROP TRIGGER IF EXISTS trg_pago_upd            ON pago;
DROP TRIGGER IF EXISTS trg_orden_upd           ON orden;
DROP TRIGGER IF EXISTS trg_producto_upd        ON producto;
DROP TRIGGER IF EXISTS trg_cafeteria_upd       ON cafeteria;
DROP TRIGGER IF EXISTS trg_usuario_upd         ON usuario;

DROP FUNCTION IF EXISTS fn_validar_detalle();
DROP FUNCTION IF EXISTS fn_cancelar_pago();
DROP FUNCTION IF EXISTS fn_historial_orden();
DROP FUNCTION IF EXISTS fn_validar_transicion_orden();
DROP FUNCTION IF EXISTS fn_set_actualizado_en();
