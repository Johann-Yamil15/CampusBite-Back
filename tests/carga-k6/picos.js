// Pico: la hora del almuerzo. Pocos usuarios, de pronto muchos (todos abren el menú a la vez) y de vuelta a pocos.
// Comprueba que el servicio aguanta el salto y SE RECUPERA (la latencia vuelve a la normal después del pico).
//   k6 run -e JWT_KEY=<llave del back local> tests/carga-k6/picos.js
import http from 'k6/http';
import { check, sleep } from 'k6';
import { BASE_URL, USUARIOS, UMBRALES_LECTURA, cabeceras, firmarJwt, resumen, TENDENCIA } from './lib/comun.js';

export const options = {
  summaryTrendStats: TENDENCIA,
  scenarios: {
    almuerzo: {
      executor: 'ramping-vus', startVUs: 5, gracefulRampDown: '10s',
      stages: [
        { duration: '30s', target: 5 },     // calma
        { duration: '10s', target: 150 },   // el pico llega de golpe
        { duration: '60s', target: 150 },   // se sostiene
        { duration: '10s', target: 5 },     // pasa
        { duration: '45s', target: 5 },     // recuperación: aquí la latencia debe volver a la normal
      ],
    },
  },
  thresholds: { ...UMBRALES_LECTURA, 'http_req_duration{tipo:lectura}': ['p(95)<1500'] },
};

export function setup() {
  return { alumno: firmarJwt(USUARIOS.alumno, 3600) };
}

export default function (t) {
  const r = http.get(`${BASE_URL}/api/menu`, { ...cabeceras(t.alumno), tags: { tipo: 'lectura' } });
  check(r, { 'menú 200': (x) => x.status === 200 });
  sleep(1 + Math.random());
}

export const handleSummary = resumen('picos');
