-- JJ-Sprint5 13/11/2026: ENUM nuevos del rediseño (promociones, dietas de producto, reportes de pedido)
DO $$
BEGIN
  IF to_regtype('public.tipo_promocion') IS NULL THEN
    CREATE TYPE tipo_promocion AS ENUM ('porcentaje','precio_fijo','combo');
  END IF;
  IF to_regtype('public.dieta') IS NULL THEN
    CREATE TYPE dieta AS ENUM ('vegetariano','sin_gluten');
  END IF;
  IF to_regtype('public.motivo_reporte') IS NULL THEN
    CREATE TYPE motivo_reporte AS ENUM ('falto_articulo','producto_equivocado','calidad','cobro','otro');
  END IF;
END $$;
