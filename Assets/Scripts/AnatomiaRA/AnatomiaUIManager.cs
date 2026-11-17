using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

public enum Pantalla
{
    Carga, Inicio, Instrucciones, Escaneo, ErrorEscaneo, VistaAR, Capas, Info, ComoUsar, Acerca, CamaraDenegada
}

/// <summary>
/// Controla las pantallas del diseño de Figma y las conecta con el pulmón en RA (ARLungController).
/// Lo crea y configura el menú "Anatomía RA > Construir interfaz en la escena".
/// </summary>
public class AnatomiaUIManager : MonoBehaviour
{
    [Header("Realidad aumentada")]
    public ARLungController controlador;
    public ARSession sesion;
    public ARPlaneManager planos;

    [Header("Pantallas")]
    public GameObject pantallaCarga;
    public GameObject pantallaInicio;
    public GameObject pantallaInstrucciones;
    public GameObject pantallaEscaneo;
    public GameObject pantallaErrorEscaneo;
    public GameObject pantallaVistaAR;
    public GameObject pantallaCapas;
    public GameObject pantallaInfo;
    public GameObject pantallaComoUsar;
    public GameObject pantallaAcerca;
    public GameObject pantallaCamaraDenegada;

    [Header("01 — Carga")]
    public RectTransform barraProgreso;
    public float anchoBarra = 206f;
    public float duracionCarga = 2.2f;

    [Header("04 — Escaneo")]
    public TMP_Text escaneoTitulo;
    public TMP_Text escaneoMensaje;
    public TMP_Text escaneoAyuda;
    public GameObject escaneoMarco;
    [Tooltip("Segundos sin detectar superficie antes de mostrar el error.")]
    public float tiempoMaximoEscaneo = 25f;

    [Header("05 — Vista AR")]
    public RectTransform contenedorEtiquetas;
    public TMP_Text textoBotonEtiquetas;
    public Sprite spriteRedondeado;

    [Header("06 — Capas")]
    public Toggle toggleTraquea;
    public Toggle toggleBronquios;
    public Toggle toggleLobulos;
    public Toggle toggleSegmentos;
    public Slider sliderTransparencia;

    [Header("07 — Información")]
    public TMP_Text infoTitulo;
    public TMP_Text infoDescripcion;
    public TMP_Text infoUbicacion;
    public TMP_Text textoBotonAislar;

    [Header("Aviso")]
    public GameObject aviso;
    public TMP_Text avisoTexto;

    static readonly Color ROJO = new Color32(0xE8, 0x1A, 0x40, 0xFF);
    static readonly Color TEXTO = new Color32(0x1F, 0x26, 0x36, 0xFF);
    static readonly Color GRIS = new Color32(0x61, 0x6E, 0x80, 0xFF);

    Pantalla actual = Pantalla.Carga;
    Pantalla anterior = Pantalla.Inicio;
    Dictionary<Pantalla, GameObject> pantallas;
    float inicioEscaneo;
    bool etiquetasActivas = true;
    bool arSoportado = true;
    EstructuraAnatomica seleccionada;
    Coroutine rutinaAviso;
    volatile int resultadoPermiso; // 0 = nada, 1 = concedido, 2 = denegado

    class Etiqueta
    {
        public RectTransform caja, linea;
        public TipoEstructura tipo;
        public int lado;     // 0 centro, +1 derecho del paciente, -1 izquierdo
        public float ny;     // altura normalizada en la pieza
    }
    readonly List<Etiqueta> etiquetas = new List<Etiqueta>();

    // ---------------------------------------------------------------- Ciclo de vida

    void Awake()
    {
        pantallas = new Dictionary<Pantalla, GameObject>
        {
            [Pantalla.Carga] = pantallaCarga,
            [Pantalla.Inicio] = pantallaInicio,
            [Pantalla.Instrucciones] = pantallaInstrucciones,
            [Pantalla.Escaneo] = pantallaEscaneo,
            [Pantalla.ErrorEscaneo] = pantallaErrorEscaneo,
            [Pantalla.VistaAR] = pantallaVistaAR,
            [Pantalla.Capas] = pantallaCapas,
            [Pantalla.Info] = pantallaInfo,
            [Pantalla.ComoUsar] = pantallaComoUsar,
            [Pantalla.Acerca] = pantallaAcerca,
            [Pantalla.CamaraDenegada] = pantallaCamaraDenegada,
        };

        if (controlador == null) controlador = FindFirstObjectByType<ARLungController>(FindObjectsInactive.Include);
        if (sesion == null) sesion = FindFirstObjectByType<ARSession>(FindObjectsInactive.Include);
        if (planos == null) planos = FindFirstObjectByType<ARPlaneManager>(FindObjectsInactive.Include);

        DetenerAR();
    }

    void Start()
    {
        foreach (var b in GetComponentsInChildren<BotonAccion>(true))
        {
            var accion = b.accion;
            b.GetComponent<Button>().onClick.AddListener(() => Ejecutar(accion));
        }

        if (toggleTraquea) toggleTraquea.onValueChanged.AddListener(v => controlador?.SetCapaVisible(CapaAnatomica.Traquea, v));
        if (toggleBronquios) toggleBronquios.onValueChanged.AddListener(v => controlador?.SetCapaVisible(CapaAnatomica.Bronquios, v));
        if (toggleLobulos) toggleLobulos.onValueChanged.AddListener(v => controlador?.SetCapaVisible(CapaAnatomica.Lobulos, v));
        if (toggleSegmentos) toggleSegmentos.onValueChanged.AddListener(v => controlador?.SetCapaVisible(CapaAnatomica.Segmentos, v));
        if (sliderTransparencia) sliderTransparencia.onValueChanged.AddListener(v => controlador?.SetOpacidad(1f - v * 0.85f));

        if (controlador)
        {
            controlador.ModeloColocado += AlColocarModelo;
            controlador.EstructuraTocada += AlTocarEstructura;
        }

        CrearEtiquetas();
        ActualizarBotonEtiquetas();
        if (aviso) aviso.SetActive(false);

        StartCoroutine(RutinaCarga());
    }

    void OnDestroy()
    {
        if (controlador)
        {
            controlador.ModeloColocado -= AlColocarModelo;
            controlador.EstructuraTocada -= AlTocarEstructura;
        }
    }

    void Update()
    {
        ProcesarPermiso();

        // Botón "atrás" de Android.
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Atras();

        if (actual == Pantalla.Escaneo)
        {
            ActualizarEscaneo();
            bool sinPlanos = controlador == null || controlador.PlanosDetectados == 0;
            if (sinPlanos && Time.time - inicioEscaneo > tiempoMaximoEscaneo)
                Mostrar(Pantalla.ErrorEscaneo);
        }
    }

    void LateUpdate() => ActualizarEtiquetas();

    void OnApplicationFocus(bool foco)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Al volver de Ajustes con el permiso ya concedido.
        if (foco && actual == Pantalla.CamaraDenegada && Permission.HasUserAuthorizedPermission(Permission.Camera))
            IniciarEscaneo(true);
#endif
    }

    IEnumerator RutinaCarga()
    {
        Mostrar(Pantalla.Carga);
        StartCoroutine(VerificarAR());
        float t = 0f;
        while (t < duracionCarga)
        {
            t += Time.deltaTime;
            if (barraProgreso)
            {
                var s = barraProgreso.sizeDelta;
                s.x = anchoBarra * Mathf.SmoothStep(0f, 1f, t / duracionCarga);
                barraProgreso.sizeDelta = s;
            }
            yield return null;
        }
        Mostrar(Pantalla.Inicio);
    }

    IEnumerator VerificarAR()
    {
        yield return ARSession.CheckAvailability();
        arSoportado = ARSession.state != ARSessionState.Unsupported;
    }

    // ---------------------------------------------------------------- Navegación

    public void Mostrar(Pantalla p)
    {
        if (p == Pantalla.ComoUsar && actual != Pantalla.ComoUsar) anterior = actual;
        actual = p;
        foreach (var kv in pantallas)
            if (kv.Value) kv.Value.SetActive(kv.Key == p);

        bool enAR = p == Pantalla.VistaAR || p == Pantalla.Capas || p == Pantalla.Info;
        if (controlador)
        {
            controlador.permitirColocar = p == Pantalla.Escaneo;
            controlador.permitirInteraccion = enAR;
        }
        if (p == Pantalla.Escaneo)
        {
            inicioEscaneo = Time.time;
            ActualizarEscaneo();
        }
        if (p == Pantalla.Capas) SincronizarCapas();
        if (p == Pantalla.Info) ActualizarBotonAislar();
    }

    public void Ejecutar(AccionUI accion)
    {
        switch (accion)
        {
            case AccionUI.IrInicio:
            case AccionUI.CancelarEscaneo:
                controlador?.QuitarModelo();
                DetenerAR();
                Mostrar(Pantalla.Inicio);
                break;
            case AccionUI.IniciarExperiencia: Mostrar(Pantalla.Instrucciones); break;
            case AccionUI.ComoUsar: Mostrar(Pantalla.ComoUsar); break;
            case AccionUI.Acerca: Mostrar(Pantalla.Acerca); break;
            case AccionUI.Atras: Atras(); break;
            case AccionUI.PermitirCamara: SolicitarCamara(); break;
            case AccionUI.EscanearDeNuevo: IniciarEscaneo(true); break;
            case AccionUI.ResetVista: controlador?.ResetearVista(); break;
            case AccionUI.AbrirCapas: Mostrar(Pantalla.Capas); break;
            case AccionUI.AlternarEtiquetas:
                etiquetasActivas = !etiquetasActivas;
                ActualizarBotonEtiquetas();
                break;
            case AccionUI.ExplorarEstructura:
                MostrarAviso("Toca una parte del pulmón para ver su información.");
                break;
            case AccionUI.MostrarTodo:
                controlador?.MostrarTodo();
                SincronizarCapas();
                ActualizarBotonAislar();
                break;
            case AccionUI.AislarEstructura:
                if (controlador == null) break;
                controlador.Aislar(controlador.Aislada != null ? null : seleccionada);
                ActualizarBotonAislar();
                break;
            case AccionUI.CerrarInfo: Mostrar(Pantalla.VistaAR); break;
            case AccionUI.Entendido: Mostrar(anterior); break;
            case AccionUI.AbrirAjustes: AbrirAjustesApp(); break;
        }
    }

    void Atras()
    {
        switch (actual)
        {
            case Pantalla.Instrucciones:
            case Pantalla.Acerca:
            case Pantalla.CamaraDenegada:
                Mostrar(Pantalla.Inicio);
                break;
            case Pantalla.Escaneo:
            case Pantalla.ErrorEscaneo:
                controlador?.QuitarModelo();
                DetenerAR();
                Mostrar(Pantalla.Instrucciones);
                break;
            case Pantalla.VistaAR:
                IniciarEscaneo(false); // quitar el modelo y volver a colocarlo
                break;
            case Pantalla.Capas:
            case Pantalla.Info:
                Mostrar(Pantalla.VistaAR);
                break;
            case Pantalla.ComoUsar:
                Mostrar(anterior);
                break;
        }
    }

    // ---------------------------------------------------------------- Cámara y AR

    void SolicitarCamara()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            IniciarEscaneo(true);
            return;
        }
        var callbacks = new PermissionCallbacks();
        callbacks.PermissionGranted += _ => resultadoPermiso = 1;
        callbacks.PermissionDenied += _ => resultadoPermiso = 2;
        Permission.RequestUserPermission(Permission.Camera, callbacks);
#else
        IniciarEscaneo(true);
#endif
    }

    void ProcesarPermiso()
    {
        int r = resultadoPermiso;
        if (r == 0) return;
        resultadoPermiso = 0;
        if (r == 1) IniciarEscaneo(true);
        else Mostrar(Pantalla.CamaraDenegada);
    }

    void IniciarEscaneo(bool reiniciarSesion)
    {
        if (!arSoportado) MostrarAviso("Este dispositivo podría no ser compatible con ARCore.");
        controlador?.QuitarModelo();
        if (sesion)
        {
            sesion.enabled = true;
            if (reiniciarSesion) sesion.Reset();
        }
        if (ARLungController.HayDeteccionDePlanos())
        {
            controlador?.MostrarPlanos(true);
            if (planos) planos.enabled = true;
        }
        else
        {
#if UNITY_EDITOR
            Debug.LogWarning("Anatomía RA: no hay proveedor XR activo en el editor. Activa 'XR Simulation' en Project Settings > XR Plug-in Management (pestaña de PC).");
            MostrarAviso("Activa XR Simulation para probar en el editor.", 4f);
#else
            MostrarAviso("La realidad aumentada no está disponible en este dispositivo.", 4f);
#endif
        }
        Mostrar(Pantalla.Escaneo);
    }

    void DetenerAR()
    {
        if (controlador) controlador.MostrarPlanos(false);
        if (planos) planos.enabled = false;
        if (sesion) sesion.enabled = false;
    }

    void ActualizarEscaneo()
    {
        bool hay = controlador != null && controlador.PlanosDetectados > 0;
        if (escaneoTitulo) escaneoTitulo.text = hay ? "Superficie detectada" : "Detectando superficie";
        if (escaneoMensaje) escaneoMensaje.text = hay ? "Toca el recuadro para colocar el modelo" : "Mueve el celular lentamente";
        if (escaneoAyuda) escaneoAyuda.text = hay ? "El pulmón aparecerá donde toques." : "Presiona el recuadro cuando aparezca sobre la superficie.";
        if (escaneoMarco) escaneoMarco.SetActive(!hay);
    }

    void AbrirAjustesApp()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var actividad = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var uriClase = new AndroidJavaClass("android.net.Uri"))
            {
                string paquete = actividad.Call<string>("getPackageName");
                using (var uri = uriClase.CallStatic<AndroidJavaObject>("parse", "package:" + paquete))
                using (var intent = new AndroidJavaObject("android.content.Intent", "android.settings.APPLICATION_DETAILS_SETTINGS", uri))
                {
                    intent.Call<AndroidJavaObject>("addFlags", 0x10000000);
                    actividad.Call("startActivity", intent);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("No se pudieron abrir los ajustes: " + e.Message);
        }
#else
        MostrarAviso("Los ajustes del dispositivo solo se abren en Android.");
#endif
    }

    // ---------------------------------------------------------------- Modelo

    void AlColocarModelo(GameObject modelo)
    {
        seleccionada = null;
        SincronizarCapas();
        Mostrar(Pantalla.VistaAR);
        MostrarAviso("Arrastra para girar · pellizca para acercar.");
    }

    void AlTocarEstructura(EstructuraAnatomica e, Vector3 punto)
    {
        AbrirInfo(e.IdInfoEnPunto(punto), e);
    }

    public void AbrirInfo(string id, EstructuraAnatomica e)
    {
        var ficha = DatosAnatomia.Obtener(id);
        if (infoTitulo) infoTitulo.text = ficha.titulo;
        if (infoDescripcion) infoDescripcion.text = ficha.descripcion;
        if (infoUbicacion) infoUbicacion.text = ficha.ubicacion;
        seleccionada = e != null ? e : controlador?.BuscarEstructura(DatosAnatomia.TipoParaId(id));
        Mostrar(Pantalla.Info);
    }

    void SincronizarCapas()
    {
        if (controlador == null) return;
        var c = controlador.CapasVisibles;
        if (toggleTraquea) toggleTraquea.SetIsOnWithoutNotify((c & CapaAnatomica.Traquea) != 0);
        if (toggleBronquios) toggleBronquios.SetIsOnWithoutNotify((c & CapaAnatomica.Bronquios) != 0);
        if (toggleLobulos) toggleLobulos.SetIsOnWithoutNotify((c & CapaAnatomica.Lobulos) != 0);
        if (toggleSegmentos) toggleSegmentos.SetIsOnWithoutNotify((c & CapaAnatomica.Segmentos) != 0);
        if (sliderTransparencia) sliderTransparencia.SetValueWithoutNotify((1f - controlador.Opacidad) / 0.85f);
    }

    void ActualizarBotonAislar()
    {
        if (textoBotonAislar)
            textoBotonAislar.text = controlador != null && controlador.Aislada != null ? "Mostrar todo" : "Aislar estructura";
    }

    void ActualizarBotonEtiquetas()
    {
        if (textoBotonEtiquetas) textoBotonEtiquetas.color = etiquetasActivas ? ROJO : TEXTO;
    }

    // ---------------------------------------------------------------- Etiquetas sobre el modelo

    void CrearEtiquetas()
    {
        if (contenedorEtiquetas == null) return;
        CrearEtiqueta("Tráquea", "traquea", TipoEstructura.ViaAerea, 0, 0.9f);
        CrearEtiqueta("Lóbulo superior derecho", "lobulo_superior_derecho", TipoEstructura.Pulmones, +1, 0.78f);
        CrearEtiqueta("Lóbulo inferior izquierdo", "lobulo_inferior_izquierdo", TipoEstructura.Pulmones, -1, 0.22f);
    }

    void CrearEtiqueta(string texto, string id, TipoEstructura tipo, int lado, float ny)
    {
        var lineaGO = new GameObject("Linea " + texto, typeof(RectTransform), typeof(Image));
        lineaGO.layer = gameObject.layer;
        var linea = (RectTransform)lineaGO.transform;
        linea.SetParent(contenedorEtiquetas, false);
        linea.pivot = new Vector2(0f, 0.5f);
        var imgLinea = lineaGO.GetComponent<Image>();
        imgLinea.color = new Color(TEXTO.r, TEXTO.g, TEXTO.b, 0.85f);
        imgLinea.raycastTarget = false;

        var cajaGO = new GameObject("Etiqueta " + texto, typeof(RectTransform), typeof(Image), typeof(Button));
        cajaGO.layer = gameObject.layer;
        var caja = (RectTransform)cajaGO.transform;
        caja.SetParent(contenedorEtiquetas, false);
        var img = cajaGO.GetComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.92f);
        if (spriteRedondeado)
        {
            img.sprite = spriteRedondeado;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 64f / 10f;
        }
        cajaGO.GetComponent<Button>().onClick.AddListener(() => AbrirInfo(id, null));

        var textoGO = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
        textoGO.layer = gameObject.layer;
        var rtTexto = (RectTransform)textoGO.transform;
        rtTexto.SetParent(caja, false);
        rtTexto.anchorMin = Vector2.zero;
        rtTexto.anchorMax = Vector2.one;
        rtTexto.offsetMin = new Vector2(8f, 4f);
        rtTexto.offsetMax = new Vector2(-8f, -4f);
        var tmp = textoGO.GetComponent<TextMeshProUGUI>();
        tmp.text = texto;
        tmp.fontSize = 12f;
        tmp.color = GRIS;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;
        Vector2 tam = tmp.GetPreferredValues(texto, 96f, 0f);
        caja.sizeDelta = new Vector2(Mathf.Min(tam.x, 96f) + 16f, tam.y + 8f);

        etiquetas.Add(new Etiqueta { caja = caja, linea = linea, tipo = tipo, lado = lado, ny = ny });
        lineaGO.SetActive(false);
        cajaGO.SetActive(false);
    }

    void ActualizarEtiquetas()
    {
        if (etiquetas.Count == 0) return;
        Camera cam = controlador != null && controlador.camaraAR != null ? controlador.camaraAR : Camera.main;
        bool mostrar = etiquetasActivas && actual == Pantalla.VistaAR && controlador != null && controlador.ModeloActual != null && cam != null;

        Vector2 centroLocal = Vector2.zero;
        if (mostrar)
        {
            Vector3 centro = cam.WorldToScreenPoint(controlador.ModeloActual.transform.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(contenedorEtiquetas, centro, null, out centroLocal);
        }

        foreach (var et in etiquetas)
        {
            var e = mostrar ? controlador.BuscarEstructura(et.tipo) : null;
            bool visible = e != null && e.Visible;
            Vector3 sp = Vector3.zero;
            if (visible)
            {
                float nx = et.lado == 0 ? 0.5f : (et.lado > 0 ? e.XLadoDerecho : 1f - e.XLadoDerecho);
                sp = cam.WorldToScreenPoint(e.PuntoNormalizado(nx, et.ny, 0.5f));
                visible = sp.z > 0f;
            }
            if (et.caja.gameObject.activeSelf != visible)
            {
                et.caja.gameObject.SetActive(visible);
                et.linea.gameObject.SetActive(visible);
            }
            if (!visible) continue;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(contenedorEtiquetas, sp, null, out Vector2 punto);
            float dir = et.lado == 0 ? -1f : (punto.x < centroLocal.x ? -1f : 1f);
            Vector2 posCaja = punto + new Vector2(dir * 100f, et.ny > 0.5f ? 34f : -34f);
            et.caja.localPosition = posCaja;

            Vector2 d = posCaja - punto;
            et.linea.localPosition = punto;
            et.linea.sizeDelta = new Vector2(d.magnitude, 1.5f);
            et.linea.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }
    }

    // ---------------------------------------------------------------- Aviso

    public void MostrarAviso(string mensaje, float segundos = 2.8f)
    {
        if (aviso == null) return;
        if (avisoTexto) avisoTexto.text = mensaje;
        if (rutinaAviso != null) StopCoroutine(rutinaAviso);
        rutinaAviso = StartCoroutine(RutinaAviso(segundos));
    }

    IEnumerator RutinaAviso(float segundos)
    {
        aviso.SetActive(true);
        yield return new WaitForSeconds(segundos);
        aviso.SetActive(false);
        rutinaAviso = null;
    }
}
