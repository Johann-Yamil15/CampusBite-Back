// Estrés: sube la llegada de peticiones por escalones HASTA encontrar dónde se degrada el servicio (no hay un objetivo
// "correcto": se busca el punto de quiebre). Los umbrales abortan la prueba cuando el servicio ya no responde bien.
//   k6 run -e JWT_KEY=<llave del back local> tests/carga-k6/estres.js
import http from 'k6/http';
import { check } from 'k6';
import { BASE_URL, CAFETERIA_ID, USUARIOS, cabeceras, firmarJwt, resumen, TENDENCIA } from './lib/comun.js';

export const options = {
  summaryTrendStats: TENDENCIA,
  scenarios: {
    escalones: {
      executor: 'ramping-arrival-rate',
      startRate: 10, timeUnit: '1s', preAllocatedVUs: 50, maxVUs: 500,
      stages: [
        { target: 25, duration: '30s' },
        { target: 50, duration: '30s' },
        { target: 100, duration: '30s' },
        { target: 200, duration: '30s' },
        { target: 300, duration: '30s' },
        { target: 0, duration: '15s' },
      ],
    },
  },
  thresholds: {
    // Si más del 5 % falla o el p95 pasa de 2 s, se corta: ese escalón es el límite.
    http_req_failed: [{ threshold: 'rate<0.05', abortOnFail: true, delayAbortEval: '20s' }],
    http_req_duration: [{ threshold: 'p(95)<2000', abortOnFail: true, delayAbortEval: '20s' }],
  },
};

export function setup() {
  return { alumno: firmarJwt(USUARIOS.alumno, 3600), encargado: firmarJwt(USUARIOS.encargado, 3600) };
}

export default function (t) {
  const quien = Math.random() < 0.8 ? 'alumno' : 'encargado';
  const url = quien === 'alumno'
    ? `${BASE_URL}/api/menu`
    : `${BASE_URL}/api/cafeterias/${CAFETERIA_ID}/productos?pagina=1&tamanoPagina=100`;
  const r = http.get(url, { ...cabeceras(t[quien]), tags: { tipo: 'lectura', quien } });
  check(r, { '200': (x) => x.status === 200 });
}

export const handleSummary = resumen('estres');
