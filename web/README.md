# Anatomía RA — Versión Web

Visor 3D interactivo del sistema respiratorio (laringe, tráquea con árbol bronquial, pulmones y glándula tiroides) sobre fondo negro.
Proyecto PY001 · Ingeniería de Software II · Universidad Nacional del Santa · Equipo InnoMed.

## Estructura

```
AnatomiaRA-Web/
├── index.html              # Estructura de la página (portada, estructuras, cómo usar, RA, acerca)
├── css/
│   └── estilos.css         # Estilos: tema oscuro, tarjetas de vidrio, responsive (móvil ≤ 900px)
├── js/
│   └── app.js              # Visor 3D: Three.js, carga del modelo, selección, capas, hotspots, miniaturas
├── assets/
│   ├── modelos/pulmon.glb  # Modelo 3D (GLB optimizado, 4.6 MB)
│   └── img/logo_uns.png    # Logo de la UNS
├── package.json            # Script "npm start" para servidor local
└── AnatomiaRA-Web.code-workspace  # Ábrelo con doble clic: configura Live Server y extensiones
```

## Cómo ejecutarlo en VS Code

El modelo 3D se carga con `fetch`, así que **no funciona abriendo `index.html` con doble clic**; necesita un servidor local.

**Opción 1 — Live Server (recomendada)**
1. Haz doble clic en `AnatomiaRA-Web.code-workspace` (o en VS Code: `Archivo → Abrir carpeta…`).
2. Acepta instalar las extensiones recomendadas (aparece un aviso abajo a la derecha), o instala **Live Server** de Ritwick Dey.
3. Clic derecho en `index.html` → **Open with Live Server** (o el botón *Go Live* de la barra inferior).

**Opción 2 — Terminal (necesita Node.js)**
```bash
npm start
```
Luego abre http://localhost:5500

**Opción 3 — Python**
```bash
python -m http.server 5500
```

## Tecnologías

| Tecnología | Uso |
|---|---|
| HTML5 + CSS3 | Estructura y diseño (glassmorphism, `clamp()`, grid, media queries) |
| JavaScript (ES Modules) | Lógica de la interfaz |
| Three.js 0.170 (CDN jsDelivr) | Render WebGL, `GLTFLoader`, `OrbitControls`, `RoomEnvironment` |
| glTF / GLB | Formato del modelo 3D (exportado desde Blender) |
| Google Fonts | Inter e Inter Tight |

## Dónde cambiar cosas

- **Textos de cada estructura:** objeto `PARTES` en `js/app.js`.
- **Colores de las partes grises:** `COLORES_MATERIAL` en `js/app.js`.
- **Tarjetas flotantes (hotspots):** `HOTSPOTS` en `js/app.js`.
- **Cambiar el modelo:** reemplaza `assets/modelos/pulmon.glb` (los nombres de nodo esperados son `laringe`, `traquea`, `pulmon_derecho`, `pulmon_izquierdo`, `glandula_tiroides`).
- **Colores del tema:** variables `--bg`, `--red`, etc. al inicio de `css/estilos.css`.

> Requiere conexión a internet la primera vez, porque Three.js y las fuentes se cargan desde CDN.
