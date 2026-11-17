using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// Menú "Anatomía RA > Construir interfaz en la escena".
/// Arma en la escena abierta las pantallas del diseño de Figma "Anatomía RA — Prototipo Frontend"
/// (medidas 390 x 844) con uGUI + TextMeshPro, y las conecta con ARLungController.
/// Se puede volver a ejecutar: borra la interfaz anterior y la crea de nuevo.
/// </summary>
public static class ConstructorUIAnatomiaRA
{
    const string RAIZ = "Assets/AnatomiaRA";
    const string SPRITES = RAIZ + "/Sprites";
    const string GENERADOS = SPRITES + "/Generados";
    const string MATERIALES = RAIZ + "/Materiales";
    const string NOMBRE_UI = "AnatomiaRA_UI";
    const float RADIO_SPRITE = 64f;
    const float ALTO = 844f;

    // Colores del diseño
    static readonly Color FONDO = Hex("#F6F8FA");
    static readonly Color BLANCO = Color.white;
    static readonly Color BORDE = Hex("#DBE0E8");
    static readonly Color ROJO = Hex("#E81A40");
    static readonly Color TEXTO = Hex("#1F2636");
    static readonly Color TITULO = Hex("#141C2B");
    static readonly Color GRIS = Hex("#616E80");
    static readonly Color CIRCULO = Hex("#E8EDF3");
    static readonly Color SELECTOR = Hex("#E8EBEE");
    static readonly Color PISTA = Hex("#E8ECF2");

    static Sprite sRedondeado, sCheck, sPlay, sRefresh, sFlecha, sMover, sMarco;
    static Sprite sLogo, sPulmon, sError, sCamara;
    static TMP_FontAsset fuente;

    enum Estilo { Primario, Secundario, Plano, Circulo, Blanco }
    enum BotonDerecho { Ninguno, Ayuda, Reset }

    // =====================================================================================
    [MenuItem("Anatomía RA/Construir interfaz en la escena")]
    public static void Construir()
    {
        if (Resources.Load<TMP_Settings>("TMP Settings") == null)
        {
            EditorUtility.DisplayDialog("Anatomía RA",
                "Falta importar los recursos de TextMeshPro.\n\nVe a Window > TextMeshPro > Import TMP Essential Resources y luego vuelve a ejecutar este menú.",
                "Entendido");
            return;
        }
        fuente = TMP_Settings.defaultFontAsset;

        var controlador = Object.FindFirstObjectByType<ARLungController>(FindObjectsInactive.Include);
        if (controlador == null)
        {
            EditorUtility.DisplayDialog("Anatomía RA",
                "No encontré ARLungController en la escena abierta.\nAbre la escena con tu XR Origin (SampleScene) y vuelve a intentarlo.",
                "Entendido");
            return;
        }
        var sesion = Object.FindFirstObjectByType<ARSession>(FindObjectsInactive.Include);
        var planos = Object.FindFirstObjectByType<ARPlaneManager>(FindObjectsInactive.Include);

        try
        {
            EditorUtility.DisplayProgressBar("Anatomía RA", "Generando imágenes…", 0.2f);
            CrearCarpetas();
            GenerarSprites();

            EditorUtility.DisplayProgressBar("Anatomía RA", "Preparando el modelo del pulmón…", 0.45f);
            PrepararModelo(controlador);

            EditorUtility.DisplayProgressBar("Anatomía RA", "Construyendo pantallas…", 0.7f);
            var previa = GameObject.Find(NOMBRE_UI);
            if (previa != null) Object.DestroyImmediate(previa);
            AsegurarEventSystem();

            var canvasGO = new GameObject(NOMBRE_UI, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGO.layer = 5;
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390, ALTO);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
            scaler.referencePixelsPerUnit = 100f;
            canvasGO.AddComponent<AjusteCanvasMovil>();

            var ui = canvasGO.AddComponent<AnatomiaUIManager>();
            ui.controlador = controlador;
            ui.sesion = sesion;
            ui.planos = planos;
            ui.spriteRedondeado = sRedondeado;

            var t = canvasGO.transform;
            ui.pantallaCarga = PantallaCarga(t, ui);
            ui.pantallaInicio = PantallaInicio(t);
            ui.pantallaInstrucciones = PantallaInstrucciones(t);
            ui.pantallaEscaneo = PantallaEscaneo(t, ui);
            ui.pantallaErrorEscaneo = PantallaErrorEscaneo(t);
            ui.pantallaVistaAR = PantallaVistaAR(t, ui);
            ui.pantallaCapas = PantallaCapas(t, ui);
            ui.pantallaInfo = PantallaInfo(t, ui);
            ui.pantallaComoUsar = PantallaComoUsar(t);
            ui.pantallaAcerca = PantallaAcerca(t);
            ui.pantallaCamaraDenegada = PantallaCamaraDenegada(t);
            Aviso(t, ui);

            // Solo la pantalla de carga visible al abrir la escena.
            foreach (var p in new[] { ui.pantallaInicio, ui.pantallaInstrucciones, ui.pantallaEscaneo, ui.pantallaErrorEscaneo,
                                      ui.pantallaVistaAR, ui.pantallaCapas, ui.pantallaInfo, ui.pantallaComoUsar,
                                      ui.pantallaAcerca, ui.pantallaCamaraDenegada })
                p.SetActive(false);

            // La sesión AR arranca cuando el usuario pulsa "Permitir cámara", no al abrir la app.
            if (sesion) { sesion.enabled = false; EditorUtility.SetDirty(sesion); }
            if (planos) { planos.enabled = false; EditorUtility.SetDirty(planos); }

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            MejorarCalidadRender();

            Undo.RegisterCreatedObjectUndo(canvasGO, "Construir interfaz Anatomía RA");
            EditorSceneManager.MarkSceneDirty(canvasGO.scene);
            EditorSceneManager.SaveScene(canvasGO.scene);
            Selection.activeGameObject = canvasGO;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        EditorUtility.DisplayDialog("Anatomía RA",
            "Interfaz creada y escena guardada.\n\n• 11 pantallas del diseño de Figma en \"AnatomiaRA_UI\".\n• El pulmón ya tiene colisiones y piezas seleccionables.\n• Orientación: vertical.\n\nDale Play (con XR Simulation) o haz la build de Android.",
            "Listo");
    }

    // =====================================================================================
    // Pantallas

    static GameObject PantallaCarga(Transform canvas, AnatomiaUIManager ui)
    {
        var (raiz, c) = NuevaPantalla(canvas, "01 — Carga", false);
        var logo = Img(c, "Logo UNS", BLANCO, 0, sLogo); Arriba(logo.rectTransform, 86, 150, 218, 218);
        Arriba(Txt(c, "Titulo", "Anatomía RA", 24, TITULO, true).rectTransform, 0, 392, 390, 30);
        Arriba(Txt(c, "Subtitulo", "Árbol bronquial y pulmonar", 15, GRIS).rectTransform, 0, 428, 390, 20);
        var pista = Img(c, "Barra", PISTA, 4); Arriba(pista.rectTransform, 92, 486, 206, 8);
        var relleno = Img(pista.transform, "Progreso", ROJO, 4); Arriba(relleno.rectTransform, 0, 0, 0, 8);
        ui.barraProgreso = relleno.rectTransform;
        ui.anchoBarra = 206f;
        Arriba(Txt(c, "Estado", "Preparando la experiencia…", 13, GRIS).rectTransform, 0, 518, 390, 18);
        Abajo(Txt(c, "Pie", "UNS · Medicina Humana", 12, GRIS).rectTransform, 0, 780, 390, 16);
        return raiz;
    }

    static GameObject PantallaInicio(Transform canvas)
    {
        var (raiz, c) = NuevaPantalla(canvas, "02 — Inicio", false);
        Encabezado(c, "Anatomía RA", false, BotonDerecho.Ayuda);

        var tarjeta = Img(c, "Tarjeta", BLANCO, 24); Arriba(tarjeta.rectTransform, 20, 102, 350, 310);
        Arriba(Txt(c, "Titulo", "Explora el sistema respiratorio", 24, TITULO, true).rectTransform, 66, 147, 279, 62);
        Arriba(Txt(c, "Descripcion", "Visualiza estructuras anatómicas en realidad aumentada.", 14, GRIS).rectTransform, 20, 275, 170, 70);
        var pulmon = Img(c, "Pulmon", BLANCO, 0, sPulmon); Arriba(pulmon.rectTransform, 200, 225, 144, 146);

        var iniciar = Boton(c, "Iniciar experiencia", "", Estilo.Primario, AccionUI.IniciarExperiencia);
        Arriba((RectTransform)iniciar.transform, 28, 454, 334, 54);
        ContenidoConIcono(iniciar.transform, sPlay, "Iniciar experiencia", BLANCO);

        Arriba((RectTransform)Boton(c, "Como usar", "¿Cómo usar la app?", Estilo.Secundario, AccionUI.ComoUsar).transform, 28, 526, 162, 48);
        Arriba((RectTransform)Boton(c, "Acerca", "Acerca del proyecto", Estilo.Secundario, AccionUI.Acerca).transform, 200, 526, 162, 48);
        Arriba(Txt(c, "Lema", "Escanea · Explora · Aprende", 13, GRIS).rectTransform, 0, 632, 390, 18);
        return raiz;
    }

    static GameObject PantallaInstrucciones(Transform canvas)
    {
        var (raiz, c) = NuevaPantalla(canvas, "03 — Instrucciones", false);
        Encabezado(c, "Antes de empezar", true, BotonDerecho.Ninguno);
        Arriba(Txt(c, "Titulo", "Prepara tu espacio", 24, TITULO, true, TextAlignmentOptions.TopLeft).rectTransform, 24, 110, 342, 30);
        Arriba(Txt(c, "Subtitulo", "Sigue estos 3 pasos para colocar el modelo.", 14, GRIS, false, TextAlignmentOptions.TopLeft).rectTransform, 24, 146, 342, 18);

        string[,] pasos =
        {
            { "01", "Busca una mesa o superficie plana", "Apoya el celular con buena iluminación." },
            { "02", "Mueve el celular lentamente", "Recorre la superficie hasta detectarla." },
            { "03", "Toca para colocar el modelo", "Aparecerá el pulmón en realidad aumentada." },
        };
        float[] ys = { 205, 317, 429 };
        for (int i = 0; i < 3; i++)
        {
            var circulo = Img(c, "Paso " + pasos[i, 0], ROJO, 21); Arriba(circulo.rectTransform, 24, ys[i], 42, 42);
            var num = Txt(circulo.transform, "Numero", pasos[i, 0], 13, BLANCO, true, TextAlignmentOptions.Center); Estirar(num.rectTransform);
            Arriba(Txt(c, "Paso titulo", pasos[i, 1], 15, TEXTO, true, TextAlignmentOptions.TopLeft).rectTransform, 82, ys[i] + 1, 284, 20);
            Arriba(Txt(c, "Paso texto", pasos[i, 2], 12, GRIS, false, TextAlignmentOptions.TopLeft).rectTransform, 82, ys[i] + 25, 284, 16);
        }

        Abajo((RectTransform)Boton(c, "Permitir camara", "Permitir cámara", Estilo.Primario, AccionUI.PermitirCamara).transform, 24, 654, 342, 54);
        Abajo(Txt(c, "Nota", "La cámara se usa solo para detectar la superficie.", 11, GRIS).rectTransform, 0, 734, 390, 14);
        return raiz;
    }

    static GameObject PantallaEscaneo(Transform canvas, AnatomiaUIManager ui)
    {
        // Pantalla transparente: se ve la cámara para escanear la mesa.
        var (raiz, c) = NuevaPantalla(canvas, "04 — Escaneo", true);

        var marco = Img(c, "Marco", new Color(1f, 1f, 1f, 0.9f), 0, sMarco);
        Centro(marco.rectTransform, 0, 90, 170, 170);
        ui.escaneoMarco = marco.gameObject;

        var pildora = Img(c, "Estado", BLANCO, 18); Arriba(pildora.rectTransform, 85, 96, 220, 36);
        var titulo = Txt(pildora.transform, "Titulo", "Detectando superficie", 15, TITULO, true, TextAlignmentOptions.Center);
        Estirar(titulo.rectTransform);
        ui.escaneoTitulo = titulo;

        var panel = Img(c, "Panel inferior", FONDO); Abajo(panel.rectTransform, -600, 470, 1590, ALTO - 470 + 300);
        panel.raycastTarget = true;
        var tarjeta = Img(c, "Tarjeta", BLANCO, 24); Abajo(tarjeta.rectTransform, 20, 484, 350, 124);
        var msg = Txt(c, "Mensaje", "Mueve el celular lentamente", 15, TEXTO, false); Abajo(msg.rectTransform, 30, 500, 330, 20);
        ui.escaneoMensaje = msg;
        Abajo(Txt(c, "Consejo", "Busca una zona plana y bien iluminada.", 12, GRIS).rectTransform, 30, 526, 330, 16);
        var ayuda = Txt(c, "Ayuda", "Presiona el recuadro cuando aparezca sobre la superficie.", 12, GRIS); Abajo(ayuda.rectTransform, 40, 556, 310, 34);
        ui.escaneoAyuda = ayuda;

        Abajo((RectTransform)Boton(c, "Cancelar", "Cancelar", Estilo.Secundario, AccionUI.CancelarEscaneo).transform, 115, 646, 160, 50);

        Encabezado(c, "Escanear superficie", true, BotonDerecho.Ayuda);
        return raiz;
    }

    static GameObject PantallaErrorEscaneo(Transform canvas)
    {
        var (raiz, c) = NuevaPantalla(canvas, "04.1 — Error de escaneo", false);
        Encabezado(c, "Escanear superficie", true, BotonDerecho.Ayuda);
        var tarjeta = Img(c, "Tarjeta", BLANCO, 24); Arriba(tarjeta.rectTransform, 20, 148, 350, 458);
        Arriba(Txt(c, "Titulo", "Superficie no detectada", 18, TITULO, true).rectTransform, 20, 171, 350, 24);
        var icono = Img(c, "Icono error", BLANCO, 0, sError); Arriba(icono.rectTransform, 80, 205, 230, 230);
        Arriba(Txt(c, "Mensaje", "Vuelve a escanear", 15, GRIS).rectTransform, 0, 486, 390, 20);
        Arriba(Txt(c, "Consejo", "Busca una zona plana y bien iluminada.", 12, GRIS).rectTransform, 0, 521, 390, 16);
        Abajo((RectTransform)Boton(c, "Cancelar", "Cancelar", Estilo.Secundario, AccionUI.CancelarEscaneo).transform, 24, 650, 160, 50);
        Abajo((RectTransform)Boton(c, "Escanear de nuevo", "Escanear de nuevo", Estilo.Primario, AccionUI.EscanearDeNuevo).transform, 206, 650, 160, 50);
        return raiz;
    }

    static GameObject PantallaVistaAR(Transform canvas, AnatomiaUIManager ui)
    {
        // Transparente: en el centro se ve el pulmón en RA (en Figma era la imagen de ejemplo).
        var (raiz, c) = NuevaPantalla(canvas, "05 — Vista AR", true);

        var etiquetas = Nuevo("Etiquetas", raiz.transform);
        Estirar(etiquetas);
        etiquetas.SetAsFirstSibling();
        ui.contenedorEtiquetas = etiquetas;

        var panel = Img(c, "Panel inferior", FONDO); Abajo(panel.rectTransform, -600, 630, 1590, ALTO - 630 + 300);
        panel.raycastTarget = true;

        var selector = Img(c, "Seleccion capa", SELECTOR, 18); Abajo(selector.rectTransform, 16, 646, 358, 76);
        var capas = Boton(selector.transform, "Capas", "Capas", Estilo.Blanco, AccionUI.AbrirCapas, 18, 13);
        Arriba((RectTransform)capas.transform, 12, 7, 160, 62);
        var etq = Boton(selector.transform, "Etiquetas", "Etiquetas", Estilo.Blanco, AccionUI.AlternarEtiquetas, 18, 13);
        Arriba((RectTransform)etq.transform, 186, 7, 160, 62);
        ui.textoBotonEtiquetas = etq.GetComponentInChildren<TextMeshProUGUI>();

        Abajo((RectTransform)Boton(c, "Explorar estructura", "Explorar estructura", Estilo.Primario, AccionUI.ExplorarEstructura).transform, 24, 748, 342, 50);

        Encabezado(c, "Pulmón", true, BotonDerecho.Reset);
        return raiz;
    }

    static GameObject PantallaCapas(Transform canvas, AnatomiaUIManager ui)
    {
        var (raiz, c) = NuevaPantalla(canvas, "06 — Panel de capas", true);
        var fondo = Img(c, "Fondo", FONDO); Arriba(fondo.rectTransform, -600, 290, 1590, 2000);
        fondo.raycastTarget = true;

        var hoja = Img(c, "Capas anatomicas", BORDE, 22); Arriba(hoja.rectTransform, 16, 300, 358, 416);
        var h = Img(hoja.transform, "Interior", BLANCO, 21); Estirar(h.rectTransform, 1, 1, 1, 1);
        var s = h.transform;
        Arriba(Txt(s, "Titulo", "Capas anatómicas", 18, TITULO, true, TextAlignmentOptions.TopLeft).rectTransform, 15, 25, 300, 24);
        Arriba(Txt(s, "Subtitulo", "Selecciona qué estructuras visualizar", 12, GRIS, false, TextAlignmentOptions.TopLeft).rectTransform, 15, 53, 300, 16);
        ui.toggleTraquea = Casilla(s, "Traquea", "Tráquea", 15, 96);
        ui.toggleBronquios = Casilla(s, "Bronquios", "Bronquios principales", 15, 144);
        ui.toggleLobulos = Casilla(s, "Lobulos", "Lóbulos", 15, 192);
        ui.toggleSegmentos = Casilla(s, "Segmentos", "Segmentos", 15, 240);
        Arriba(Txt(s, "Transparencia", "Transparencia", 13, TEXTO, true, TextAlignmentOptions.TopLeft).rectTransform, 15, 295, 300, 18);
        ui.sliderTransparencia = Deslizador(s, 15, 314);
        Arriba((RectTransform)Boton(s, "Mostrar todo", "Mostrar todo", Estilo.Secundario, AccionUI.MostrarTodo).transform, 15, 365, 300, 42);

        Arriba(Txt(c, "Pie", "Vista actual · Modelo pulmonar", 11, GRIS).rectTransform, 0, 770, 390, 14);
        Encabezado(c, "Pulmón", true, BotonDerecho.Reset);
        return raiz;
    }

    static GameObject PantallaInfo(Transform canvas, AnatomiaUIManager ui)
    {
        var (raiz, c) = NuevaPantalla(canvas, "07 — Información de estructura", true);
        var fondo = Img(c, "Fondo", FONDO); Arriba(fondo.rectTransform, -600, 290, 1590, 2000);
        fondo.raycastTarget = true;

        // Hoja que crece hacia abajo según el texto.
        var hoja = Img(c, "Ficha", BORDE, 22);
        var rh = hoja.rectTransform;
        rh.anchorMin = new Vector2(0, 0); rh.anchorMax = new Vector2(1, 1); rh.pivot = new Vector2(0.5f, 1);
        rh.offsetMin = new Vector2(16, 24); rh.offsetMax = new Vector2(-16, -300);
        var interior = Img(hoja.transform, "Interior", BLANCO, 21); Estirar(interior.rectTransform, 1, 1, 1, 1);

        var lista = interior.gameObject.AddComponent<VerticalLayoutGroup>();
        lista.padding = new RectOffset(15, 15, 25, 20);
        lista.spacing = 12;
        lista.childControlWidth = true; lista.childControlHeight = true;
        lista.childForceExpandWidth = true; lista.childForceExpandHeight = false;
        var s = interior.transform;

        ui.infoTitulo = Txt(s, "Titulo", "Lóbulo superior derecho", 17, TITULO, true, TextAlignmentOptions.TopLeft);
        ui.infoDescripcion = Txt(s, "Descripcion", "Se encuentra en la parte superior del pulmón derecho y participa en el intercambio de oxígeno y dióxido de carbono.", 14, TEXTO, false, TextAlignmentOptions.TopLeft);
        Txt(s, "Ubicacion titulo", "Ubicación y relaciones", 14, TEXTO, true, TextAlignmentOptions.TopLeft);
        ui.infoUbicacion = Txt(s, "Ubicacion", "", 13, GRIS, false, TextAlignmentOptions.TopLeft);
        var espacio = Nuevo("Espacio", s); espacio.gameObject.AddComponent<LayoutElement>().minHeight = 4;

        var aislar = Boton(s, "Aislar estructura", "Aislar estructura", Estilo.Secundario, AccionUI.AislarEstructura);
        aislar.gameObject.AddComponent<LayoutElement>().preferredHeight = 46;
        ui.textoBotonAislar = aislar.GetComponentInChildren<TextMeshProUGUI>();
        var cerrar = Boton(s, "Cerrar", "Cerrar", Estilo.Primario, AccionUI.CerrarInfo);
        cerrar.gameObject.AddComponent<LayoutElement>().preferredHeight = 46;
        Txt(s, "Pista", "Toca una estructura para consultar su información.", 12, GRIS);

        Encabezado(c, "Pulmón", true, BotonDerecho.Reset);
        return raiz;
    }

    static GameObject PantallaComoUsar(Transform canvas)
    {
        var (raiz, c) = NuevaPantalla(canvas, "08 — Cómo usar", false);
        Encabezado(c, "Cómo usar", true, BotonDerecho.Ninguno);
        var filas = new (Sprite icono, string texto)[]
        {
            (sRefresh, "1 dedo: rotar"),
            (sFlecha, "2 dedos: zoom"),
            (sMover, "Toca una estructura para ver su información"),
            (sRefresh, "Restablece la vista"),
        };
        float[] ys = { 127, 213, 299, 385 };
        for (int i = 0; i < filas.Length; i++)
        {
            var circ = Img(c, "Icono " + (i + 1), CIRCULO, 17); Arriba(circ.rectTransform, 28, ys[i], 34, 34);
            var ic = Img(circ.transform, "Simbolo", TEXTO, 0, filas[i].icono); Centro(ic.rectTransform, 0, 0, 16, 16);
            Arriba(Txt(c, "Texto " + (i + 1), filas[i].texto, 15, TEXTO, false, TextAlignmentOptions.TopLeft).rectTransform, 78, ys[i] + 1, 270, 40);
        }
        Abajo((RectTransform)Boton(c, "Entendido", "Entendido", Estilo.Primario, AccionUI.Entendido).transform, 24, 680, 342, 52);
        Abajo(Txt(c, "Nota", "Puedes volver a consultar estas instrucciones desde el inicio.", 11, GRIS).rectTransform, 24, 752, 342, 14);
        return raiz;
    }

    static GameObject PantallaAcerca(Transform canvas)
    {
        var (raiz, c) = NuevaPantalla(canvas, "09 — Acerca del proyecto", false);
        Encabezado(c, "Acerca del proyecto", true, BotonDerecho.Ninguno);
        var logo = Img(c, "Logo UNS", BLANCO, 0, sLogo); Arriba(logo.rectTransform, 144, 135, 102, 102);
        Arriba(Txt(c, "Codigo", "PY001", 14, TEXTO, true).rectTransform, 0, 258, 390, 18);
        Arriba(Txt(c, "Nombre", "Anatomía RA", 22, TITULO, true).rectTransform, 0, 284, 390, 28);
        Arriba(Txt(c, "Curso", "Ingeniería de Software II", 14, TEXTO).rectTransform, 0, 350, 390, 18);
        Arriba(Txt(c, "Universidad", "Universidad Nacional del Santa", 14, TEXTO).rectTransform, 0, 378, 390, 18);
        var linea = Img(c, "Separador", BORDE); Arriba(linea.rectTransform, 105, 432, 180, 1);
        Arriba(Txt(c, "Equipo", "Equipo InnoMed", 15, TEXTO, true).rectTransform, 0, 472, 390, 20);
        Arriba(Txt(c, "Version", "Versión 1.0", 13, GRIS).rectTransform, 0, 500, 390, 18);
        Arriba(Txt(c, "Nota", "Prototipo académico · 2026", 12, GRIS).rectTransform, 0, 548, 390, 16);
        Abajo((RectTransform)Boton(c, "Volver al inicio", "Volver al inicio", Estilo.Primario, AccionUI.IrInicio).transform, 24, 680, 342, 52);
        return raiz;
    }

    static GameObject PantallaCamaraDenegada(Transform canvas)
    {
        var (raiz, c) = NuevaPantalla(canvas, "10 — Cámara denegada", false);
        Encabezado(c, "Cámara", true, BotonDerecho.Ninguno);
        var tarjeta = Img(c, "Tarjeta", BLANCO, 24); Arriba(tarjeta.rectTransform, 20, 118, 350, 500);
        var icono = Img(c, "Icono camara", BLANCO, 0, sCamara); Arriba(icono.rectTransform, 130, 218, 130, 130);
        Arriba(Txt(c, "Mensaje", "Se necesita acceso a la cámara para usar la realidad aumentada", 17, TEXTO).rectTransform, 50, 350, 290, 90);
        Arriba(Txt(c, "Consejo", "Habilita el permiso desde ajustes del dispositivo.", 12, GRIS).rectTransform, 30, 461, 330, 16);
        Abajo((RectTransform)Boton(c, "Abrir ajustes", "Abrir ajustes", Estilo.Primario, AccionUI.AbrirAjustes).transform, 24, 650, 342, 52);
        Abajo((RectTransform)Boton(c, "Volver", "Volver", Estilo.Secundario, AccionUI.IrInicio).transform, 24, 716, 342, 46);
        return raiz;
    }

    static void Aviso(Transform canvas, AnatomiaUIManager ui)
    {
        var aviso = Img(canvas, "Aviso", new Color(TEXTO.r, TEXTO.g, TEXTO.b, 0.92f), 20);
        var r = aviso.rectTransform;
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
        r.pivot = new Vector2(0.5f, 0f);
        r.anchoredPosition = new Vector2(0, 230);
        r.sizeDelta = new Vector2(320, 44);
        var t = Txt(aviso.transform, "Texto", "", 13, BLANCO, false, TextAlignmentOptions.Center);
        Estirar(t.rectTransform, 14, 4, 14, 4);
        ui.aviso = aviso.gameObject;
        ui.avisoTexto = t;
        aviso.gameObject.SetActive(false);
    }

    // =====================================================================================
    // Piezas reutilizables

    static (GameObject raiz, RectTransform columna) NuevaPantalla(Transform canvas, string nombre, bool transparente)
    {
        var raiz = Nuevo(nombre, canvas, typeof(Image));
        Estirar(raiz);
        var img = raiz.GetComponent<Image>();
        img.color = transparente ? new Color(0, 0, 0, 0) : FONDO;
        img.raycastTarget = !transparente;

        var segura = Nuevo("AreaSegura", raiz, typeof(AreaSegura));
        Estirar(segura);
        var col = Nuevo("Columna", segura);
        col.anchorMin = new Vector2(0.5f, 0f);
        col.anchorMax = new Vector2(0.5f, 1f);
        col.pivot = new Vector2(0.5f, 0.5f);
        col.sizeDelta = new Vector2(390, 0);
        col.anchoredPosition = Vector2.zero;
        return (raiz.gameObject, col);
    }

    static void Encabezado(RectTransform c, string titulo, bool atras, BotonDerecho derecho)
    {
        // Se extiende hacia arriba y a los lados para cubrir la barra de estado y pantallas anchas.
        var fondo = Img(c, "Encabezado", BLANCO); Arriba(fondo.rectTransform, -600, -300, 1590, 378);
        fondo.raycastTarget = true;
        var linea = Img(c, "Linea encabezado", BORDE); Arriba(linea.rectTransform, -600, 77, 1590, 1);

        if (atras)
        {
            var b = Boton(c, "Atras", "‹", Estilo.Plano, AccionUI.Atras, 14, 32);
            b.GetComponentInChildren<TextMeshProUGUI>().fontStyle = FontStyles.Normal;
            Arriba((RectTransform)b.transform, 10, 16, 40, 46);
        }
        Arriba(Txt(c, "Titulo encabezado", titulo, 18, TEXTO, true, TextAlignmentOptions.MidlineLeft).rectTransform, atras ? 58 : 24, 24, 270, 30);

        if (derecho == BotonDerecho.Ayuda)
        {
            var b = Boton(c, "Ayuda", "?", Estilo.Circulo, AccionUI.ComoUsar, 19, 14);
            Arriba((RectTransform)b.transform, 344, 20, 38, 38);
        }
        else if (derecho == BotonDerecho.Reset)
        {
            var b = Boton(c, "Restablecer vista", "", Estilo.Circulo, AccionUI.ResetVista, 19);
            Arriba((RectTransform)b.transform, 344, 20, 38, 38);
            var ic = Img(b.transform, "Icono", TEXTO, 0, sRefresh); Centro(ic.rectTransform, 0, 0, 16, 16);
        }
    }

    static Button Boton(Transform padre, string nombre, string texto, Estilo estilo, AccionUI accion, float radio = 14, float tamTexto = 14)
    {
        var rt = Nuevo("Boton " + nombre, padre, typeof(Image), typeof(Button), typeof(BotonAccion));
        var img = rt.GetComponent<Image>();
        Redondear(img, radio);
        Image objetivo = img;
        switch (estilo)
        {
            case Estilo.Primario: img.color = ROJO; break;
            case Estilo.Plano: img.color = new Color(1, 1, 1, 0); break;
            case Estilo.Circulo: img.color = CIRCULO; break;
            case Estilo.Blanco: img.color = BLANCO; break;
            case Estilo.Secundario:
                img.color = BORDE;
                objetivo = Img(rt, "Interior", BLANCO, radio - 1);
                Estirar(objetivo.rectTransform, 1, 1, 1, 1);
                break;
        }

        var btn = rt.GetComponent<Button>();
        btn.targetGraphic = objetivo;
        var colores = btn.colors;
        colores.normalColor = Color.white;
        colores.highlightedColor = Color.white;
        colores.selectedColor = Color.white;
        colores.pressedColor = new Color(0.82f, 0.82f, 0.85f);
        colores.fadeDuration = 0.08f;
        btn.colors = colores;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        rt.GetComponent<BotonAccion>().accion = accion;

        if (!string.IsNullOrEmpty(texto))
        {
            var t = Txt(rt, "Texto", texto, tamTexto, estilo == Estilo.Primario ? BLANCO : TEXTO, true, TextAlignmentOptions.Center);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            Estirar(t.rectTransform, 4, 0, 4, 0);
        }
        return btn;
    }

    static void ContenidoConIcono(Transform boton, Sprite icono, string texto, Color color)
    {
        var fila = Nuevo("Contenido", boton, typeof(HorizontalLayoutGroup));
        Estirar(fila);
        var hl = fila.GetComponent<HorizontalLayoutGroup>();
        hl.childAlignment = TextAnchor.MiddleCenter;
        hl.spacing = 10;
        hl.childControlWidth = true; hl.childControlHeight = true;
        hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;

        var ic = Img(fila, "Icono", color, 0, icono);
        var le = ic.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 12; le.preferredHeight = 12;
        var t = Txt(fila, "Texto", texto, 14, color, true, TextAlignmentOptions.Center);
        t.textWrappingMode = TextWrappingModes.NoWrap;
    }

    static Toggle Casilla(Transform padre, string nombre, string etiqueta, float x, float y)
    {
        var rt = Nuevo("Casilla " + nombre, padre, typeof(Image), typeof(Toggle));
        Arriba(rt, x, y, 300, 26);
        rt.GetComponent<Image>().color = new Color(1, 1, 1, 0); // zona táctil de toda la fila

        var caja = Img(rt, "Caja", BORDE, 6); Arriba(caja.rectTransform, 0, 2, 22, 22);
        var interior = Img(caja.transform, "Interior", BLANCO, 5); Estirar(interior.rectTransform, 1, 1, 1, 1);
        var marca = Img(caja.transform, "Marca", ROJO, 6); Estirar(marca.rectTransform);
        var check = Img(marca.transform, "Check", BLANCO, 0, sCheck); Centro(check.rectTransform, 0, 0, 14, 14);
        Arriba(Txt(rt, "Etiqueta", etiqueta, 14, TEXTO, false, TextAlignmentOptions.MidlineLeft).rectTransform, 36, 0, 260, 26);

        var tg = rt.GetComponent<Toggle>();
        tg.targetGraphic = caja;
        tg.graphic = marca;
        tg.toggleTransition = Toggle.ToggleTransition.None;
        tg.isOn = true;
        tg.navigation = new Navigation { mode = Navigation.Mode.None };
        return tg;
    }

    static Slider Deslizador(Transform padre, float x, float y)
    {
        var rt = Nuevo("Slider transparencia", padre, typeof(Image), typeof(Slider));
        Arriba(rt, x, y, 300, 22);
        rt.GetComponent<Image>().color = new Color(1, 1, 1, 0);

        var pista = Img(rt, "Pista", BORDE, 3);
        var rp = pista.rectTransform;
        rp.anchorMin = new Vector2(0, 0.5f); rp.anchorMax = new Vector2(1, 0.5f);
        rp.pivot = new Vector2(0.5f, 0.5f); rp.sizeDelta = new Vector2(0, 6); rp.anchoredPosition = Vector2.zero;

        var area = Nuevo("Area manija", rt);
        Estirar(area, 11, 0, 11, 0);
        var manija = Img(area, "Manija", ROJO, 11);
        manija.rectTransform.sizeDelta = new Vector2(22, 0);

        var s = rt.GetComponent<Slider>();
        s.handleRect = manija.rectTransform;
        s.targetGraphic = manija;
        s.direction = Slider.Direction.LeftToRight;
        s.minValue = 0; s.maxValue = 1; s.value = 0;
        s.navigation = new Navigation { mode = Navigation.Mode.None };
        return s;
    }

    // =====================================================================================
    // Utilidades de layout (coordenadas de Figma: x, y desde arriba-izquierda del frame 390 x 844)

    static RectTransform Nuevo(string nombre, Transform padre, params System.Type[] componentes)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.layer = 5;
        foreach (var c in componentes) go.AddComponent(c);
        var rt = (RectTransform)go.transform;
        rt.SetParent(padre, false);
        return rt;
    }

    static void Arriba(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static void Abajo(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
        rt.pivot = new Vector2(0, 0);
        rt.anchoredPosition = new Vector2(x, ALTO - y - h);
        rt.sizeDelta = new Vector2(w, h);
    }

    static void Centro(RectTransform rt, float dx, float dy, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(dx, dy);
        rt.sizeDelta = new Vector2(w, h);
    }

    static void Estirar(RectTransform rt, float izq = 0, float arr = 0, float der = 0, float aba = 0)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(izq, aba);
        rt.offsetMax = new Vector2(-der, -arr);
    }

    static Image Img(Transform padre, string nombre, Color color, float radio = 0, Sprite sprite = null)
    {
        var rt = Nuevo(nombre, padre, typeof(Image));
        var img = rt.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        if (sprite != null)
        {
            img.sprite = sprite;
            img.preserveAspect = true;
        }
        else if (radio > 0) Redondear(img, radio);
        return img;
    }

    static void Redondear(Image img, float radio)
    {
        img.sprite = sRedondeado;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = RADIO_SPRITE / Mathf.Max(radio, 0.5f);
    }

    static TextMeshProUGUI Txt(Transform padre, string nombre, string texto, float tam, Color color, bool negrita = false,
                               TextAlignmentOptions alineacion = TextAlignmentOptions.Top)
    {
        var rt = Nuevo(nombre, padre, typeof(TextMeshProUGUI));
        var t = rt.GetComponent<TextMeshProUGUI>();
        if (fuente) t.font = fuente;
        t.text = texto;
        t.fontSize = tam;
        t.color = color;
        t.fontStyle = negrita ? FontStyles.Bold : FontStyles.Normal;
        t.alignment = alineacion;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var c);
        return c;
    }

    // =====================================================================================
    // Escena, modelo y recursos

    /// <summary>Resolución completa (Render Scale 1) y antialiasing 4x en todos los niveles de calidad.</summary>
    static void MejorarCalidadRender()
    {
        var assets = new HashSet<UniversalRenderPipelineAsset>();
        if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset porDefecto) assets.Add(porDefecto);
        for (int i = 0; i < QualitySettings.names.Length; i++)
            if (QualitySettings.GetRenderPipelineAssetAt(i) is UniversalRenderPipelineAsset urp) assets.Add(urp);

        foreach (var urp in assets)
        {
            urp.renderScale = 1f;
            urp.msaaSampleCount = 4;
            EditorUtility.SetDirty(urp);
            AgregarFondoAR(urp);
        }
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Anatomía RA/Arreglar cámara AR (fondo amarillo o negro)")]
    public static void ArreglarCamaraAR()
    {
        MejorarCalidadRender();
        EditorUtility.DisplayDialog("Anatomía RA",
            "Listo: los renderers de URP ya tienen el \"AR Background Renderer Feature\".\nLa imagen de la cámara se verá en el celular. Vuelve a generar el APK.",
            "Listo");
    }

    /// <summary>
    /// Sin el AR Background Renderer Feature, URP no dibuja la imagen de la cámara de ARCore
    /// (la pantalla queda de un color sólido). Lo agrega a todos los renderers del asset.
    /// </summary>
    static void AgregarFondoAR(UniversalRenderPipelineAsset urp)
    {
        var soUrp = new SerializedObject(urp);
        var lista = soUrp.FindProperty("m_RendererDataList");
        if (lista == null) return;

        for (int i = 0; i < lista.arraySize; i++)
        {
            var datos = lista.GetArrayElementAtIndex(i).objectReferenceValue as ScriptableRendererData;
            if (datos == null) continue;

            bool yaTiene = false;
            foreach (var f in datos.rendererFeatures)
                if (f is ARBackgroundRendererFeature) { yaTiene = true; break; }
            if (yaTiene) continue;

            var feature = ScriptableObject.CreateInstance<ARBackgroundRendererFeature>();
            feature.name = "ARBackgroundRendererFeature";
            AssetDatabase.AddObjectToAsset(feature, datos);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long idLocal);

            var so = new SerializedObject(datos);
            var features = so.FindProperty("m_RendererFeatures");
            var mapa = so.FindProperty("m_RendererFeatureMap");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            if (mapa != null)
            {
                mapa.arraySize++;
                mapa.GetArrayElementAtIndex(mapa.arraySize - 1).longValue = idLocal;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(datos);
            Debug.Log("Anatomía RA: AR Background Renderer Feature agregado a " + datos.name);
        }
    }

    static void AsegurarEventSystem()
    {
        var es = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
        if (es == null)
        {
            var go = new GameObject("EventSystem", typeof(EventSystem));
            Undo.RegisterCreatedObjectUndo(go, "EventSystem");
            es = go.GetComponent<EventSystem>();
        }
        var viejo = es.GetComponent<StandaloneInputModule>();
        if (viejo != null) Object.DestroyImmediate(viejo);
        if (es.GetComponent<InputSystemUIInputModule>() == null) es.gameObject.AddComponent<InputSystemUIInputModule>();
    }

    static void PrepararModelo(ARLungController controlador)
    {
        var prefab = controlador.lungPrefab;
        if (prefab == null)
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Lungs/Lungs With Texture.prefab");
            if (prefab == null) { Debug.LogWarning("Anatomía RA: ARLungController no tiene prefab del pulmón."); return; }
        }

        // Mallas legibles: necesario para las colisiones al tocar el modelo en Android.
        string ruta = AssetDatabase.GetAssetPath(prefab);
        foreach (var dep in AssetDatabase.GetDependencies(ruta, true))
        {
            if (AssetImporter.GetAtPath(dep) is ModelImporter mi && !mi.isReadable)
            {
                mi.isReadable = true;
                mi.SaveAndReimport();
            }
        }

        // Si es directamente el FBX, se crea un prefab editable.
        if (PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.Model)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            ruta = RAIZ + "/Pulmon_RA.prefab";
            prefab = PrefabUtility.SaveAsPrefabAsset(inst, ruta);
            Object.DestroyImmediate(inst);
        }
        if (controlador.lungPrefab != prefab)
        {
            controlador.lungPrefab = prefab;
            EditorUtility.SetDirty(controlador);
        }

        var raiz = PrefabUtility.LoadPrefabContents(ruta);
        try
        {
            if (raiz.GetComponentsInChildren<EstructuraAnatomica>(true).Length == 0)
                EstructuraAnatomica.ConfigurarAutomaticamente(raiz);

            foreach (var e in raiz.GetComponentsInChildren<EstructuraAnatomica>(true))
            {
                foreach (var mf in e.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
                        mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                }
                var mats = new List<Material>();
                foreach (var r in e.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials) mats.Add(MaterialTransparente(m));
                e.materialesTransparentes = mats.ToArray();
            }
            PrefabUtility.SaveAsPrefabAsset(raiz, ruta);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }

    static Material MaterialTransparente(Material m)
    {
        if (m == null) return null;
        string nombre = m.name;
        foreach (char ch in Path.GetInvalidFileNameChars()) nombre = nombre.Replace(ch, '_');
        string ruta = $"{MATERIALES}/{nombre}_Transparente.mat";

        var existente = AssetDatabase.LoadAssetAtPath<Material>(ruta);
        if (existente != null)
        {
            existente.shader = m.shader;
            existente.CopyPropertiesFromMaterial(m);
            EstructuraAnatomica.HacerTransparente(existente);
            EditorUtility.SetDirty(existente);
            return existente;
        }
        var nuevo = EstructuraAnatomica.HacerTransparente(new Material(m));
        AssetDatabase.CreateAsset(nuevo, ruta);
        return nuevo;
    }

    static void CrearCarpetas()
    {
        foreach (var carpeta in new[] { RAIZ, SPRITES, GENERADOS, MATERIALES })
        {
            if (AssetDatabase.IsValidFolder(carpeta)) continue;
            string padre = Path.GetDirectoryName(carpeta).Replace('\\', '/');
            AssetDatabase.CreateFolder(padre, Path.GetFileName(carpeta));
        }
    }

    static void GenerarSprites()
    {
        sRedondeado = Guardar("redondeado", TexturaRedondeada(160, RADIO_SPRITE), new Vector4(64, 64, 64, 64));

        sCheck = Guardar("icono_check", new Pintor(128)
            .Linea(new Vector2(28, 66), new Vector2(54, 40), 16)
            .Linea(new Vector2(54, 40), new Vector2(100, 88), 16).Textura(), Vector4.zero);

        sPlay = Guardar("icono_play", new Pintor(128)
            .Triangulo(new Vector2(30, 16), new Vector2(30, 112), new Vector2(112, 64)).Textura(), Vector4.zero);

        // ⟳ círculo abierto con flecha
        var p = new Pintor(128).Arco(new Vector2(64, 64), 40, 110, 400, 12);
        float th = 110f * Mathf.Deg2Rad;
        var extremo = new Vector2(64 + 40 * Mathf.Cos(th), 64 + 40 * Mathf.Sin(th));
        var radial = new Vector2(Mathf.Cos(th), Mathf.Sin(th));
        var tangente = new Vector2(Mathf.Sin(th), -Mathf.Cos(th));
        p.Triangulo(extremo + radial * 18, extremo - radial * 18, extremo + tangente * 22);
        sRefresh = Guardar("icono_refrescar", p.Textura(), Vector4.zero);

        sFlecha = Guardar("icono_flecha", new Pintor(128)
            .Linea(new Vector2(30, 30), new Vector2(96, 96), 12)
            .Linea(new Vector2(98, 98), new Vector2(54, 98), 12)
            .Linea(new Vector2(98, 98), new Vector2(98, 54), 12).Textura(), Vector4.zero);

        sMover = Guardar("icono_mover", new Pintor(128)
            .Linea(new Vector2(30, 64), new Vector2(98, 64), 10)
            .Linea(new Vector2(64, 30), new Vector2(64, 98), 10)
            .Triangulo(new Vector2(98, 80), new Vector2(98, 48), new Vector2(120, 64))
            .Triangulo(new Vector2(30, 80), new Vector2(30, 48), new Vector2(8, 64))
            .Triangulo(new Vector2(48, 98), new Vector2(80, 98), new Vector2(64, 120))
            .Triangulo(new Vector2(48, 30), new Vector2(80, 30), new Vector2(64, 8)).Textura(), Vector4.zero);

        // Marco de escaneo (cuadrado con marcas en las esquinas, como en Figma)
        var marco = new Pintor(256);
        float a = 72, b = 184, g = 12;
        marco.Linea(new Vector2(a, a), new Vector2(b, a), g).Linea(new Vector2(b, a), new Vector2(b, b), g)
             .Linea(new Vector2(b, b), new Vector2(a, b), g).Linea(new Vector2(a, b), new Vector2(a, a), g);
        foreach (float x0 in new[] { a, b })
            foreach (float y0 in new[] { a, b })
            {
                float sx = x0 == a ? -1 : 1, sy = y0 == a ? -1 : 1;
                marco.Linea(new Vector2(x0 + sx * 24, y0), new Vector2(x0 + sx * 44, y0), g);
                marco.Linea(new Vector2(x0, y0 + sy * 24), new Vector2(x0, y0 + sy * 44), g);
            }
        sMarco = Guardar("marco_escaneo", marco.Textura(), Vector4.zero);

        // Imágenes exportadas de Figma
        sLogo = CargarImagen("logo_uns.png");
        sPulmon = CargarImagen("pulmon.png");
        sError = CargarImagen("error_escaneo.png");
        sCamara = CargarImagen("camara_denegada.png");
    }

    static Sprite CargarImagen(string archivo)
    {
        string ruta = $"{SPRITES}/{archivo}";
        if (!File.Exists(ruta)) { Debug.LogWarning("Anatomía RA: falta " + ruta); return null; }
        ConfigurarSprite(ruta, Vector4.zero);
        return AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
    }

    static Sprite Guardar(string nombre, Texture2D tex, Vector4 borde)
    {
        string ruta = $"{GENERADOS}/{nombre}.png";
        File.WriteAllBytes(ruta, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(ruta, ImportAssetOptions.ForceUpdate);
        ConfigurarSprite(ruta, borde);
        return AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
    }

    static void ConfigurarSprite(string ruta, Vector4 borde)
    {
        var imp = AssetImporter.GetAtPath(ruta) as TextureImporter;
        if (imp == null) return;
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.alphaIsTransparency = true;
        // Mipmaps + trilinear: las imágenes se ven suaves al mostrarse más pequeñas (sin pixelado).
        imp.mipmapEnabled = true;
        imp.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
        imp.spritePixelsPerUnit = 100;
        imp.spriteBorder = borde;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.filterMode = FilterMode.Trilinear;
        imp.anisoLevel = 2;
        imp.maxTextureSize = 2048;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();
    }

    static Texture2D TexturaRedondeada(int tam, float radio)
    {
        var tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false);
        var px = new Color32[tam * tam];
        for (int y = 0; y < tam; y++)
            for (int x = 0; x < tam; x++)
            {
                var punto = new Vector2(x + 0.5f, y + 0.5f);
                var cerca = new Vector2(Mathf.Clamp(punto.x, radio, tam - radio), Mathf.Clamp(punto.y, radio, tam - radio));
                float alfa = Mathf.Clamp01(radio - Vector2.Distance(punto, cerca) + 0.5f);
                px[y * tam + x] = new Color32(255, 255, 255, (byte)(alfa * 255));
            }
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }

    /// <summary>Dibuja iconos simples (blancos con antialias) usando distancias.</summary>
    class Pintor
    {
        readonly int tam;
        readonly float[] alfa;

        public Pintor(int tam)
        {
            this.tam = tam;
            alfa = new float[tam * tam];
        }

        void Aplicar(System.Func<Vector2, float> distancia)
        {
            for (int y = 0; y < tam; y++)
                for (int x = 0; x < tam; x++)
                {
                    float a = Mathf.Clamp01(0.5f - distancia(new Vector2(x + 0.5f, y + 0.5f)));
                    int i = y * tam + x;
                    if (a > alfa[i]) alfa[i] = a;
                }
        }

        public Pintor Linea(Vector2 p0, Vector2 p1, float grosor)
        {
            float h = grosor * 0.5f;
            Aplicar(p => DistSegmento(p, p0, p1) - h);
            return this;
        }

        public Pintor Arco(Vector2 c, float r, float desde, float hasta, float grosor)
        {
            float h = grosor * 0.5f;
            var e0 = c + r * new Vector2(Mathf.Cos(desde * Mathf.Deg2Rad), Mathf.Sin(desde * Mathf.Deg2Rad));
            var e1 = c + r * new Vector2(Mathf.Cos(hasta * Mathf.Deg2Rad), Mathf.Sin(hasta * Mathf.Deg2Rad));
            Aplicar(p =>
            {
                var d = p - c;
                float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                while (ang < desde) ang += 360f;
                while (ang >= desde + 360f) ang -= 360f;
                if (ang <= hasta) return Mathf.Abs(d.magnitude - r) - h;
                return Mathf.Min((p - e0).magnitude, (p - e1).magnitude) - h;
            });
            return this;
        }

        public Pintor Triangulo(Vector2 a, Vector2 b, Vector2 c)
        {
            Aplicar(p =>
            {
                float d = Mathf.Min(DistSegmento(p, a, b), Mathf.Min(DistSegmento(p, b, c), DistSegmento(p, c, a)));
                float c1 = Cruz(b - a, p - a), c2 = Cruz(c - b, p - b), c3 = Cruz(a - c, p - c);
                bool dentro = (c1 >= 0 && c2 >= 0 && c3 >= 0) || (c1 <= 0 && c2 <= 0 && c3 <= 0);
                return dentro ? -d : d;
            });
            return this;
        }

        public Texture2D Textura()
        {
            var tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false);
            var px = new Color32[tam * tam];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, (byte)(alfa[i] * 255));
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        static float Cruz(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

        static float DistSegmento(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return (p - (a + ab * t)).magnitude;
        }
    }
}
