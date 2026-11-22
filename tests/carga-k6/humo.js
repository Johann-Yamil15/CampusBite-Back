// Humo: 1 usuario virtual recorre los endpoints principales UNA vez. Sirve para comprobar que el ambiente y los datos
// están listos antes de lanzar una prueba de carga (y como verificación rápida tras un despliegue).
//   k6 run -e JWT_KEY=<llave del back local> tests/carga-k6/humo.js
import http from 'k6/http';
import { check, group } from 'k6';
import { BASE_URL, CAFETERIA_ID, USUARIOS, cabeceras, firmarJwt, resumen, TENDENCIA } from './lib/comun.js';

export const options = {
  summaryTrendStats: TENDENCIA,
  vus: 1,
  iterations: 1,
  thresholds: { http_req_failed: ['rate==0'], checks: ['rate==1'] },
};

export function setup() {
  return { alumno: firmarJwt(USUARIOS.alumno), encargado: firmarJwt(USUARIOS.encargado) };
}

export default function (t) {
  group('sin sesión', () => {
    const r = http.get(`${BASE_URL}/api/menu`, { tags: { tipo: 'lectura' }, responseCallback: http.expectedStatuses(401) });
    check(r, { 'sin token: 401': (x) => x.status === 401, 'ProblemDetails con detail': (x) => !!x.json('detail') });
  });

  group('alumno', () => {
    const menu = http.get(`${BASE_URL}/api/menu`, { ...cabeceras(t.alumno), tags: { tipo: 'lectura' } });
    check(menu, {
      'menú 200': (x) => x.status === 200,
      'menú trae la cafetería de prueba': (x) => x.json().some((c) => c.idCafeteria === CAFETERIA_ID),
    });
    const ordenes = http.get(`${BASE_URL}/api/ordenes/mias?pagina=1&tamanoPagina=50`, { ...cabeceras(t.alumno), tags: { tipo: 'lectura' } });
    check(ordenes, { 'órdenes 200 paginado': (x) => x.status === 200 && Array.isArray(x.json('items')) });
  });

  group('encargado', () => {
    const base = `${BASE_URL}/api/cafeterias/${CAFETERIA_ID}`;
    const cats = http.get(`${base}/categorias`, { ...cabeceras(t.encargado), tags: { tipo: 'lectura' } });
    check(cats, { 'categorías 200': (x) => x.status === 200 && x.json().length >= 2 });
    const prods = http.get(`${base}/productos?pagina=1&tamanoPagina=100`, { ...cabeceras(t.encargado), tags: { tipo: 'lectura' } });
    check(prods, { 'productos 200 paginado': (x) => x.status === 200 && x.json('total') >= 3 });
  });
}

export const handleSummary = resumen('humo');
