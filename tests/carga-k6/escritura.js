// Escrituras concurrentes (cuidado: ESCRIBE en la base; úsala solo contra un ambiente de pruebas desechable).
//   - Alumnos: crean un pedido en efectivo y lo cancelan.
//   - Encargados: crean un platillo (multipart, sin imagen) y lo borran.
// Cada iteración limpia lo que crea. Comprueba que las transacciones aguantan concurrencia sin errores 5xx ni duplicados.
//   k6 run -e JWT_KEY=<llave del back local> tests/carga-k6/escritura.js
import http from 'k6/http';
import { check, sleep } from 'k6';
import { BASE_URL, CAFETERIA_ID, USUARIOS, UMBRALES_ESCRITURA, cabeceras, firmarJwt, resumen, TENDENCIA } from './lib/comun.js';

const PRODUCTO_CHILAQUILES = '00000000-0000-0000-0000-0000000000e1';

export const options = {
  summaryTrendStats: TENDENCIA,
  scenarios: {
    pedidos: { executor: 'ramping-vus', exec: 'pedido', startVUs: 0, stages: [{ duration: '20s', target: 20 }, { duration: '60s', target: 20 }, { duration: '10s', target: 0 }] },
    platillos: { executor: 'ramping-vus', exec: 'platillo', startVUs: 0, stages: [{ duration: '20s', target: 10 }, { duration: '60s', target: 10 }, { duration: '10s', target: 0 }] },
  },
  thresholds: {
    ...UMBRALES_ESCRITURA,
    http_req_failed: ['rate<0.01'],
    checks: ['rate>0.99'],
  },
};

export function setup() {
  return { alumno: firmarJwt(USUARIOS.alumno, 3600), encargado: firmarJwt(USUARIOS.encargado, 3600) };
}

// Mañana a las 13:00 (hora del centro de México): la cafetería de prueba abre de 07:00 a 20:00 todos los días
function recogidaManana() {
  const mx = new Date(Date.now() - 6 * 3600 * 1000);
  mx.setUTCDate(mx.getUTCDate() + 1);
  return `${mx.toISOString().slice(0, 10)}T13:00:00-06:00`;
}

const escritura = { tipo: 'escritura' };

// multipart/form-data armado a mano: k6 solo lo genera si hay un archivo (http.file) y aquí el alta no lleva imagen
function multipart(campos) {
  const limite = `----k6${Math.random().toString(16).slice(2)}`;
  const CRLF = '\r\n';
  const partes = Object.entries(campos).map(
    ([k, v]) => `--${limite}${CRLF}Content-Disposition: form-data; name="${k}"${CRLF}${CRLF}${v}${CRLF}`,
  );
  return { cuerpo: `${partes.join('')}--${limite}--${CRLF}`, tipo: `multipart/form-data; boundary=${limite}` };
}

export function pedido(t) {
  const h = cabeceras(t.alumno);
  const alta = http.post(`${BASE_URL}/api/ordenes`, JSON.stringify({
    idCafeteria: CAFETERIA_ID, recogidaEn: recogidaManana(), metodo: 'Efectivo',
    items: [{ idProducto: PRODUCTO_CHILAQUILES, cantidad: 1 }], notas: 'carga k6',
  }), { headers: { ...h.headers, 'Content-Type': 'application/json' }, tags: { ...escritura, endpoint: 'crear_orden' } });
  const creada = check(alta, { 'orden creada (201)': (r) => r.status === 201, 'trae idOrden': (r) => !!r.json('idOrden') });
  if (creada) {
    const cancela = http.post(`${BASE_URL}/api/ordenes/${alta.json('idOrden')}/cancelar`, null, { ...h, tags: { ...escritura, endpoint: 'cancelar_orden' } });
    check(cancela, { 'orden cancelada': (r) => r.status === 200 || r.status === 204 });
  }
  sleep(1);
}

export function platillo(t) {
  const h = cabeceras(t.encargado);
  const base = `${BASE_URL}/api/cafeterias/${CAFETERIA_ID}/productos`;
  const nombre = `k6 ${__VU}-${__ITER}-${Date.now()}`;
  const form = multipart({ nombre, descripcion: 'prueba de carga', precio: '12.50', disponible: 'true' });
  const alta = http.post(base, form.cuerpo, { headers: { ...h.headers, 'Content-Type': form.tipo }, tags: { ...escritura, endpoint: 'crear_producto' } });
  const creado = check(alta, { 'platillo creado (201)': (r) => r.status === 201 });
  if (!creado && __ITER < 2) console.error(`alta de platillo: ${alta.status} ${String(alta.body).slice(0, 200)}`);
  if (creado) {
    const baja = http.del(`${base}/${alta.json('idProducto')}`, null, { ...h, tags: { ...escritura, endpoint: 'borrar_producto' } });
    check(baja, { 'platillo borrado (204)': (r) => r.status === 204 });
  }
  sleep(1);
}

export const handleSummary = resumen('escritura');
