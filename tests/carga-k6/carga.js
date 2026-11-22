// Carga normal: mezcla realista de lecturas con usuarios que "piensan" entre pantallas.
//   80 % alumnos: menú, órdenes, promociones
//   20 % encargados: categorías y productos paginados (lo que abre el panel del negocio)
// Perfil: sube a la carga objetivo, la mantiene y baja. Variables: -e VUS_ALUMNOS=40 -e VUS_ENCARGADOS=10 -e DURACION=3m
//   k6 run -e JWT_KEY=<llave del back local> tests/carga-k6/carga.js
import http from 'k6/http';
import { check, sleep } from 'k6';
import { BASE_URL, CAFETERIA_ID, USUARIOS, UMBRALES_LECTURA, cabeceras, firmarJwt, resumen, TENDENCIA } from './lib/comun.js';

const VUS_ALUMNOS = Number(__ENV.VUS_ALUMNOS || 40);
const VUS_ENCARGADOS = Number(__ENV.VUS_ENCARGADOS || 10);
const DURACION = __ENV.DURACION || '3m';

const perfil = (vus) => [
  { duration: '30s', target: vus },
  { duration: DURACION, target: vus },
  { duration: '20s', target: 0 },
];

export const options = {
  summaryTrendStats: TENDENCIA,
  scenarios: {
    alumnos: { executor: 'ramping-vus', exec: 'alumno', startVUs: 0, stages: perfil(VUS_ALUMNOS), gracefulRampDown: '10s' },
    encargados: { executor: 'ramping-vus', exec: 'encargado', startVUs: 0, stages: perfil(VUS_ENCARGADOS), gracefulRampDown: '10s' },
  },
  thresholds: UMBRALES_LECTURA,
};

export function setup() {
  return { alumno: firmarJwt(USUARIOS.alumno, 3 * 3600), encargado: firmarJwt(USUARIOS.encargado, 3 * 3600) };
}

const lectura = { tipo: 'lectura' };

export function alumno(t) {
  const h = cabeceras(t.alumno);
  const menu = http.get(`${BASE_URL}/api/menu`, { ...h, tags: { ...lectura, endpoint: 'menu' } });
  check(menu, { 'menú 200': (r) => r.status === 200, 'menú con cafeterías': (r) => Array.isArray(r.json()) });
  sleep(1 + Math.random() * 2);

  const promos = http.get(`${BASE_URL}/api/promociones/activas`, { ...h, tags: { ...lectura, endpoint: 'promociones' } });
  check(promos, { 'promociones 200': (r) => r.status === 200 });
  sleep(0.5 + Math.random());

  const ordenes = http.get(`${BASE_URL}/api/ordenes/mias?pagina=1&tamanoPagina=50`, { ...h, tags: { ...lectura, endpoint: 'ordenes_mias' } });
  check(ordenes, { 'órdenes 200': (r) => r.status === 200 });
  sleep(1 + Math.random() * 3);
}

export function encargado(t) {
  const h = cabeceras(t.encargado);
  const base = `${BASE_URL}/api/cafeterias/${CAFETERIA_ID}`;
  const cats = http.get(`${base}/categorias`, { ...h, tags: { ...lectura, endpoint: 'categorias' } });
  check(cats, { 'categorías 200': (r) => r.status === 200 });
  const prods = http.get(`${base}/productos?pagina=1&tamanoPagina=100`, { ...h, tags: { ...lectura, endpoint: 'productos' } });
  check(prods, { 'productos 200': (r) => r.status === 200, 'productos paginados': (r) => r.json('items') !== undefined });
  sleep(2 + Math.random() * 3);
}

export const handleSummary = resumen('carga');
