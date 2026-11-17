<div align="center">

<img src="docs/banner.png" alt="Anatomía RA — Árbol bronquial y pulmonar en realidad aumentada" width="100%"/>

# 🫁 Anatomía RA

**Aplicación móvil de realidad aumentada para aprender la anatomía del árbol bronquial y los pulmones.**
Coloca un pulmón 3D sobre tu mesa, gíralo, acércalo y toca cada estructura para conocerla.

<br/>

[![Unity](https://img.shields.io/badge/Unity-6.6_LTS-000000?style=for-the-badge&logo=unity&logoColor=white)](https://unity.com/)
[![C#](https://img.shields.io/badge/C%23-.NET-512BD4?style=for-the-badge&logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![AR Foundation](https://img.shields.io/badge/AR_Foundation-6.6-E81A40?style=for-the-badge&logo=unity&logoColor=white)](https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.0/manual/index.html)
[![ARCore](https://img.shields.io/badge/Google_ARCore-6.6-4285F4?style=for-the-badge&logo=google&logoColor=white)](https://developers.google.com/ar)
[![Android](https://img.shields.io/badge/Android-8.0%2B-3DDC84?style=for-the-badge&logo=android&logoColor=white)](https://www.android.com/)

[![URP](https://img.shields.io/badge/URP-17.6-1F2636?style=flat-square&logo=unity)](https://unity.com/srp/universal-render-pipeline)
[![Input System](https://img.shields.io/badge/Input_System-1.20-1F2636?style=flat-square&logo=unity)](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.20/manual/index.html)
[![Figma](https://img.shields.io/badge/Diseño-Figma-F24E1E?style=flat-square&logo=figma&logoColor=white)](https://www.figma.com/)
[![Blender](https://img.shields.io/badge/Modelado-Blender-F5792A?style=flat-square&logo=blender&logoColor=white)](https://www.blender.org/)
[![Estado](https://img.shields.io/badge/estado-prototipo_v1.0-E81A40?style=flat-square)](ROADMAP.md)
[![UNS](https://img.shields.io/badge/UNS-Ingeniería_de_Software_II-616E80?style=flat-square)](#-equipo)

[Características](#-características) ·
[Pantallas](#-pantallas) ·
[Tecnologías](#-tecnologías) ·
[Arquitectura](#-arquitectura) ·
[Instalación](#-instalación-y-compilación) ·
[Roadmap](ROADMAP.md)

</div>

---

## 📖 Sobre el proyecto

**Anatomía RA** (código **PY001**) es un prototipo académico desarrollado por el **Equipo InnoMed** para el curso de **Ingeniería de Software II** de la **Universidad Nacional del Santa (UNS)**, orientado a estudiantes de **Medicina Humana**.

Estudiar anatomía en láminas 2D hace difícil entender la forma real y las relaciones entre estructuras. Con esta app, el estudiante apunta el celular a una mesa, coloca un **modelo 3D del pulmón en realidad aumentada** y lo explora desde cualquier ángulo: rota, hace zoom, oculta capas, aísla estructuras y lee la ficha anatómica de cada parte.

> **Escanea · Explora · Aprende**

## ✨ Características

| | Funcionalidad | Descripción |
|:-:|---|---|
| 📷 | **Detección de superficies** | Encuentra mesas y pisos con ARCore y muestra el recuadro donde colocar el modelo. |
| 🫁 | **Pulmón 3D en RA** | Modelo con texturas PBR que se ajusta solo a ~30 cm y mira hacia la cámara. |
| ☝️ | **Gestos táctiles** | 1 dedo para rotar · 2 dedos para hacer zoom · toque corto para seleccionar. |
| 🏷️ | **Etiquetas anatómicas** | Tráquea, lóbulo superior derecho, lóbulo inferior izquierdo… siguen al modelo en pantalla. |
| 🧩 | **Capas anatómicas** | Muestra u oculta tráquea, bronquios, lóbulos y segmentos. |
| 🔍 | **Transparencia** | Control deslizante para ver a través del modelo. |
| 📚 | **Fichas de información** | Descripción y "ubicación y relaciones" de 7 estructuras, con opción de **aislarlas**. |
| ⟳ | **Restablecer vista** | Devuelve el modelo a su posición y tamaño iniciales. |
| 🔒 | **Permisos claros** | Flujo de permiso de cámara con pantalla de "cámara denegada" y acceso directo a Ajustes. |
| 📱 | **Diseño adaptable** | Interfaz fiel a Figma (390 × 844) que se adapta a cualquier celular y respeta la muesca. |

## 📱 Pantallas

Diseñadas en **Figma** e implementadas en Unity con uGUI + TextMeshPro (11 pantallas):

<div align="center">
<img src="docs/pantallas.png" alt="Las 11 pantallas de la aplicación" width="100%"/>
</div>

<details>
<summary><b>Ver el flujo de navegación</b></summary>

```mermaid
flowchart LR
    A([01 Carga]) --> B[02 Inicio]
    B -->|Iniciar experiencia| C[03 Instrucciones]
    B -->|¿Cómo usar?| H[08 Cómo usar]
    B -->|Acerca| I[09 Acerca del proyecto]
    C -->|Permitir cámara| P{¿Permiso?}
    P -->|Sí| D[04 Escaneo]
    P -->|No| J[10 Cámara denegada]
    J -->|Abrir ajustes| D
    D -->|Sin superficie 25 s| E[04.1 Error de escaneo]
    E -->|Escanear de nuevo| D
    D -->|Toca el recuadro| F[05 Vista AR]
    F -->|Capas| G[06 Panel de capas]
    F -->|Toca una estructura| K[07 Información]
    K -->|Aislar / Cerrar| F
    G --> F
    style F fill:#E81A40,color:#fff,stroke:#E81A40
    style A fill:#1F2636,color:#fff
```

</details>

## 🛠️ Tecnologías

### Motor y lenguaje

| Tecnología | Versión | Uso en el proyecto |
|---|---|---|
| <img src="https://cdn.simpleicons.org/unity/000000" width="16"/> **Unity** | 6.6 (`6000.6.7f1`) | Motor principal, editor y compilación |
| <img src="https://cdn.simpleicons.org/dotnet/512BD4" width="16"/> **C#** | .NET / Mono · IL2CPP | Toda la lógica de la app y del editor |

### Realidad aumentada

| Paquete | Versión | Uso |
|---|---|---|
| **AR Foundation** (`com.unity.xr.arfoundation`) | 6.6.2 | Sesión AR, planos, raycasts y fondo de cámara |
| **Google ARCore XR Plugin** (`com.unity.xr.arcore`) | 6.6.2 | Proveedor de RA en Android |
| **XR Plug-in Management** | incluido | Carga de ARCore (Android) y XR Simulation (editor) |
| **XR Simulation** | incluido | Probar la RA dentro del editor de Unity |

### Render, entrada e interfaz

| Paquete | Versión | Uso |
|---|---|---|
| **Universal Render Pipeline (URP)** | 17.6.0 | Render móvil, *AR Background Renderer Feature*, MSAA 4x |
| **Input System** (`EnhancedTouch`) | 1.20.1 | Gestos táctiles: rotar, pellizcar y tocar |
| **uGUI** + **TextMeshPro** | 2.6.0 | Las 11 pantallas, botones, casillas, slider y etiquetas |
| **UI Toolkit Figma Importer** (`com.unity.ui.figma`) | 1.0.0-pre.1 | Conexión del proyecto con el diseño de Figma |
| **Android JNI** | módulo | Permisos de cámara y acceso a Ajustes del sistema |

### Diseño y contenido 3D

| Herramienta | Uso |
|---|---|
| <img src="https://cdn.simpleicons.org/figma/F24E1E" width="16"/> **Figma** | Prototipo frontend de las 11 pantallas, colores y tipografía (Inter) |
| <img src="https://cdn.simpleicons.org/blender/F5792A" width="16"/> **Blender** | Modelado y preparación del pulmón (`pulmon_vfinal.blend`) |
| **FBX** + texturas **PBR** | Base color, normal, roughness, metalness, AO y height (2 piezas: vía aérea y pulmones) |

### Plataforma y herramientas

| Herramienta | Uso |
|---|---|
| <img src="https://cdn.simpleicons.org/android/3DDC84" width="16"/> **Android** 8.0+ (API 26) | Plataforma de destino · IL2CPP · ARM64 · OpenGL ES 3 |
| **Android SDK / NDK / JDK / Gradle** | Compilación del APK desde Unity |
| <img src="https://cdn.simpleicons.org/visualstudiocode/007ACC" width="16"/> **VS Code** · Visual Studio · Rider | Edición de scripts C# |
| <img src="https://cdn.simpleicons.org/git/F05032" width="16"/> **Git** + <img src="https://cdn.simpleicons.org/github/181717" width="16"/> **GitHub** | Control de versiones |
| <img src="https://cdn.simpleicons.org/claude/D97757" width="16"/> **Claude** (Anthropic) | Asistente de IA para implementar la interfaz y conectarla con la RA |

### Paleta de diseño

| Color | Hex | Uso |
|---|---|---|
| ![](https://img.shields.io/badge/-%20-E81A40?style=flat-square) | `#E81A40` | Botones principales, acentos |
| ![](https://img.shields.io/badge/-%20-141C2B?style=flat-square) | `#141C2B` | Títulos |
| ![](https://img.shields.io/badge/-%20-1F2636?style=flat-square) | `#1F2636` | Texto |
| ![](https://img.shields.io/badge/-%20-616E80?style=flat-square) | `#616E80` | Texto secundario |
| ![](https://img.shields.io/badge/-%20-DBE0E8?style=flat-square) | `#DBE0E8` | Bordes |
| ![](https://img.shields.io/badge/-%20-F6F8FA?style=flat-square) | `#F6F8FA` | Fondo |

## 🧱 Arquitectura

```mermaid
flowchart TB
    subgraph UI["🖼️ Interfaz (uGUI + TextMeshPro)"]
        M[AnatomiaUIManager<br/><i>pantallas, navegación, permisos, etiquetas</i>]
        B[BotonAccion] --> M
        S[AreaSegura / AjusteCanvasMovil]
    end
    subgraph AR["🫁 Realidad aumentada (AR Foundation + ARCore)"]
        C[ARLungController<br/><i>colocar, rotar, zoom, seleccionar</i>]
        E[EstructuraAnatomica<br/><i>capas, transparencia, aislar</i>]
        D[(DatosAnatomia<br/><i>fichas</i>)]
        P[ARPlaneManager · ARRaycastManager · ARSession]
    end
    M -- "Mostrar planos / Reset / Capas / Opacidad" --> C
    C -- "ModeloColocado · EstructuraTocada" --> M
    C --> E
    C --> P
    M --> D
    style C fill:#E81A40,color:#fff
    style M fill:#1F2636,color:#fff
```

| Script | Responsabilidad |
|---|---|
| [`ARLungController.cs`](Assets/Scripts/ARLungController.cs) | Coloca el pulmón sobre el plano tocado, lo escala a 30 cm, gestiona rotación, zoom, selección por toque, capas, transparencia y aislamiento. |
| [`AnatomiaUIManager.cs`](Assets/Scripts/AnatomiaRA/AnatomiaUIManager.cs) | Navegación entre las 11 pantallas, permiso de cámara, ciclo de la sesión AR, etiquetas sobre el modelo y avisos. |
| [`EstructuraAnatomica.cs`](Assets/Scripts/AnatomiaRA/EstructuraAnatomica.cs) | Marca cada pieza del modelo: tipo, capas, materiales transparentes y detección del lóbulo tocado. |
| [`DatosAnatomia.cs`](Assets/Scripts/AnatomiaRA/DatosAnatomia.cs) | Textos de las fichas: tráquea, bronquios y los 5 lóbulos. |
| [`BotonAccion.cs`](Assets/Scripts/AnatomiaRA/BotonAccion.cs) | Asocia cada botón del diseño con una acción. |
| [`AreaSegura.cs`](Assets/Scripts/AnatomiaRA/AreaSegura.cs) · [`AjusteCanvasMovil.cs`](Assets/Scripts/AnatomiaRA/AjusteCanvasMovil.cs) | Adaptación a muescas y a distintas proporciones de pantalla. |
| [`ConstructorUIAnatomiaRA.cs`](Assets/Editor/ConstructorUIAnatomiaRA.cs) | Herramienta de editor: genera toda la interfaz desde el diseño de Figma, prepara el modelo y configura URP para RA. |

<details>
<summary><b>📁 Estructura del repositorio</b></summary>

```
AnatomiaRA/
├── Assets/
│   ├── AnatomiaRA/          # Sprites (Figma + generados) y materiales transparentes
│   ├── Editor/              # ConstructorUIAnatomiaRA.cs (menú "Anatomía RA")
│   ├── Models/              # Pulmón FBX, prefabs AR, materiales y texturas PBR
│   ├── Scenes/              # SampleScene.unity (escena principal)
│   ├── Scripts/             # ARLungController + AnatomiaRA/*
│   ├── Settings/            # URP (Mobile/PC), perfil de build Android
│   ├── TextMesh Pro/        # Fuentes y shaders de TMP
│   └── XR/                  # ARCore, XR Simulation, XR Management
├── Packages/                # manifest.json y packages-lock.json
├── ProjectSettings/         # Configuración del proyecto Unity
├── docs/                    # Banner y capturas para este README
├── README.md
└── ROADMAP.md
```

</details>

## 🚀 Instalación y compilación

### Requisitos

- **Unity 6.6** (`6000.6.7f1`) con el módulo **Android Build Support** (SDK, NDK y OpenJDK)
- Celular **Android 8.0+** compatible con **ARCore** ([lista oficial](https://developers.google.com/ar/devices)) y con *Google Play Services for AR*

### Pasos

```bash
git clone https://github.com/DevonFerrer/AnatomiaRA.git
```

1. Abre la carpeta en **Unity Hub** → *Add project from disk*. La primera vez Unity reconstruye `Library/` (tarda unos minutos).
2. Abre `Assets/Scenes/SampleScene.unity`.
3. *(Opcional)* Menú **Anatomía RA → Construir interfaz en la escena** para regenerar la UI, y **Anatomía RA → Arreglar cámara AR** si la cámara se ve de un color sólido.
4. **File → Build Profiles → Android → Build** (o *Build And Run* con el celular conectado por USB).

### Probar en el editor

Con **XR Simulation** activado (Project Settings → XR Plug-in Management → pestaña PC), pulsa **Play**. El mouse funciona como dedo; mantén el **clic derecho + W A S D** para moverte por el entorno simulado.

### Uso de la app

| Gesto | Acción |
|---|---|
| ☝️ Arrastrar | Rotar el pulmón |
| 🤏 Pellizcar | Zoom |
| 👆 Toque corto | Abrir la ficha de la estructura |
| ⟳ | Restablecer la vista |

## 🗺️ Roadmap

El plan completo, con fases y estado, está en **[ROADMAP.md](ROADMAP.md)**.

- [x] Prototipo frontend en Figma (11 pantallas)
- [x] Modelo 3D del pulmón con texturas PBR
- [x] RA con AR Foundation + ARCore y gestos táctiles
- [x] Interfaz completa conectada al modelo
- [ ] Pruebas en varios dispositivos Android
- [ ] Separar el modelo en lóbulos y segmentos reales
- [ ] Modo cuestionario y más estructuras anatómicas

## 👥 Equipo

<div align="center">

| **Equipo InnoMed** |
|:-:|
| Proyecto **PY001** · Ingeniería de Software II |
| **Universidad Nacional del Santa** — Chimbote, Perú |
| Dirigido a la Escuela de **Medicina Humana** |
| Versión **1.0** · Prototipo académico · 2026 |

</div>

---

<div align="center">
<sub>Hecho con 🫁 y ☕ por el Equipo InnoMed · UNS 2026</sub>
</div>
