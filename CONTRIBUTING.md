# CampusBite — Guía de trabajo con ramas y documentación de código

Esta guía aplica a los dos repositorios: **CampusBite-Front** y **CampusBite-Back**.
Todo el equipo debe seguirla para mantener un flujo ordenado y evitar conflictos.

---

## 1. Ramas permanentes

| Rama | Propósito | ¿Push directo? |
|---|---|---|
| `develop` | Rama principal de desarrollo (es la rama por defecto del repo) | No, solo por Pull Request desde `Sprint#` |
| `Sprint1`, `Sprint2`, ... | Rama del sprint en curso. De ella salen las ramas de cada integrante | No, solo por Pull Request |
| `release` | Rama de QA (pruebas antes de producción) | No, solo por Pull Request |
| `main` | Producción | No, solo por Pull Request |

**Flujo de código:**

```
feature/bugfix  →  Sprint#  →  develop  →  release (QA)  →  main (producción)
                                                                     ↓
                                                              develop (sync)
```

Cada flecha es un **Pull Request (PR)**. Nunca se sube código directo a una rama permanente.
Después de cada merge a `main`, se hace un PR de `main` → `develop` para mantener ambas ramas sincronizadas.

---

## 2. Iniciales del equipo

Cada integrante usa las iniciales de su **primer nombre** y **apellido paterno** en el nombre de sus ramas.

| Integrante | Iniciales |
|---|---|
| Johann Yamil Jiménez Pérez | `JJ` |
| Cesar Yael López Trujillo | `CL` |
| Joseph Abrahan Yáñez García | `JY` |
| Flavio César Barreto Ortega | `FB` |

---

## 3. Convención de nombres de ramas

```
<tipo>/<INICIALES>-<Sprint#>-<HU-XX>-<descripcion-corta>
```

| Tipo | Cuándo se usa | Ejemplo |
|---|---|---|
| `feature/` | Desarrollo de una historia de usuario nueva | `feature/JJ-Sprint1-HU-04-horario-pago` |
| `bugfix/` | Corrección de un error encontrado en desarrollo o en QA | `bugfix/JJ-Sprint1-HU-04-total-incorrecto` |

Reglas:

- Todo en minúsculas después del `Sprint#`, palabras separadas con guiones (`-`).
- Sin espacios, sin acentos y sin dos puntos (`:`); Git no los acepta en nombres de rama.
- El número de la historia (`HU-XX`) debe corresponder al backlog.
- Una rama por historia. No mezclar historias distintas en una misma rama.

### Ramas de `release`

`release` es una **rama permanente de QA**, no se crea una por integrante.

- Solo el responsable del sprint hace el PR `develop` → `release`.
- Si QA encuentra un error, se corrige con una rama `bugfix/` que sale de `release` y regresa a `release` por PR. Después ese mismo arreglo se lleva también a `develop` abriendo un PR de `release` → `develop`, para que no se pierda el fix en el historial de desarrollo.
- Cuando QA aprueba, se hace el PR `release` → `main`.

---

## 4. Paso a paso para trabajar

### 4.1 Antes de empezar: traer lo más reciente

Siempre actualiza primero antes de crear tu rama, para evitar conflictos:

```bash
git checkout develop
git pull origin develop

git checkout Sprint1
git pull origin Sprint1
```

### 4.2 Crear tu rama desde la rama del sprint

```bash
git checkout Sprint1
git checkout -b feature/JJ-Sprint1-HU-04-horario-pago
git push -u origin feature/JJ-Sprint1-HU-04-horario-pago
```

Desde GitHub (navegador): selector de ramas → cambiar a `Sprint1` → escribir el nombre de la nueva rama → **Create branch ... from Sprint1**. Verifica que diga "from Sprint1" antes de confirmar.

### 4.3 Trabajar y subir cambios

```bash
git add .
git commit -m "JJ-Sprint1 28/09/2026: valida horario de recolección"
git push
```

### 4.4 Mantener tu rama al día

Antes de abrir tu PR, trae los cambios nuevos del sprint:

```bash
git checkout Sprint1
git pull origin Sprint1
git checkout feature/JJ-Sprint1-HU-04-horario-pago
git merge Sprint1
```

Resuelve conflictos, si los hay, antes de continuar.

### 4.5 Abrir el Pull Request

- **De:** tu rama `feature/...` o `bugfix/...`
- **Hacia:** `Sprint1` (nunca directo a `develop`, `release` o `main`)
- **Título:** `HU-04: Elegir horario de recolección y pagar`
- **Descripción:** qué se hizo, qué probaste y capturas si hay cambios visuales.
- Pide revisión a al menos un compañero antes de fusionar.

### 4.6 Después del merge

Puedes borrar tu rama (GitHub ofrece el botón "Delete branch") y actualizar tu copia local:

```bash
git checkout Sprint1
git pull origin Sprint1
```

---

## 5. Ramas del Sprint 1

| Integrante | Historia | Rama sugerida |
|---|---|---|
| Johann (JJ) | HU-01 Iniciar sesión y registro | `feature/JJ-Sprint1-HU-01-login-registro` |
| Johann (JJ) | HU-04 Horario de recolección y pago | `feature/JJ-Sprint1-HU-04-horario-pago` |
| Flavio (FB) | HU-02 Explorar cafeterías | `feature/FB-Sprint1-HU-02-explorar-cafeterias` |
| Joseph (JY) | HU-03 Elegir platillos y armar pedido | `feature/JY-Sprint1-HU-03-armar-pedido` |
| Cesar (CL) | HU-05 Estado del pedido y folio digital | `feature/CL-Sprint1-HU-05-folio-digital` |

Cada quien crea su rama con el mismo nombre en **ambos repositorios** (Front y Back) cuando su historia lo requiera.

---

## 6. Documentación de código

Todo bloque de código nuevo o modificado lleva un comentario breve con este formato:

```
INICIALES-Sprint# DD/MM/AAAA: descripción muy breve
```

Ejemplos:

```js
// JJ-Sprint1 28/09/2026: valida que el horario elegido sea posterior a la hora actual
function validarHorario(horario) { ... }
```

```html
<!-- CL-Sprint1 30/09/2026: línea de tiempo del estado del pedido -->
```

```css
/* FB-Sprint1 29/09/2026: estilos de las tarjetas de cafetería */
```

Reglas:

- La descripción es de **una línea**: qué hace, no cómo lo hace.
- Se pone al inicio de la función, componente, clase o bloque que agregaste o cambiaste.
- Usa la fecha del día en que hiciste el cambio.
- El mismo formato se usa en el mensaje de cada commit (ver 4.3).

---

## 7. Buenas prácticas

- Commits pequeños y frecuentes, con mensaje claro.
- Nunca subir contraseñas, llaves de API ni archivos `.env`.
- No trabajar directamente sobre `develop`, `Sprint#`, `release` ni `main`.
- Si tienes dudas sobre un conflicto, avisa al equipo antes de forzar un push (`--force` está prohibido en ramas compartidas).
- Mantén actualizado el estado de tu historia en el backlog (`Backlog_CampusBite_G3.xlsx`).

---

**CampusBite** · Universidad Tecnológica de Tula-Tepeji · Grupo 10 IDGSM G3
