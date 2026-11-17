using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

/// <summary>
/// Coloca el pulmón sobre un plano detectado y permite rotarlo (1 dedo),
/// hacer zoom (2 dedos) y tocar una estructura para ver su información.
/// Usa el Input System nuevo (el proyecto tiene desactivado el Input Manager antiguo).
/// </summary>
[RequireComponent(typeof(ARRaycastManager))]
public class ARLungController : MonoBehaviour
{
    [Header("Prefab del Pulmón")]
    public GameObject lungPrefab;

    [Header("Tamaño en realidad aumentada")]
    [Tooltip("Escala el modelo para que mida 'alturaObjetivo' metros de alto al colocarlo.")]
    public bool ajustarTamano = true;
    [Tooltip("Altura del modelo en metros (0.3 = 30 cm).")]
    public float alturaObjetivo = 0.3f;
    [Tooltip("Giro extra en grados por si el modelo aparece de espaldas.")]
    public float rotacionExtraY = 0f;

    [Header("Ajustes de Rotación y Zoom")]
    public float rotationSpeed = 0.4f;
    public float zoomSpeed = 0.005f;
    public float minScaleMultiplier = 0.5f;
    public float maxScaleMultiplier = 3.0f;

    [Header("Selección por toque")]
    [Tooltip("Un toque más largo que esto (segundos) no cuenta como selección.")]
    public float duracionMaximaToque = 0.35f;
    [Tooltip("Si el dedo se mueve más de estos píxeles no cuenta como selección.")]
    public float movimientoMaximoToque = 25f;

    [Header("Referencias (se buscan solas si están vacías)")]
    public ARPlaneManager planeManager;
    public Camera camaraAR;

    /// <summary>La interfaz decide cuándo se puede colocar y cuándo interactuar.</summary>
    [NonSerialized] public bool permitirColocar = true;
    [NonSerialized] public bool permitirInteraccion = true;

    public event Action<GameObject> ModeloColocado;
    public event Action<EstructuraAnatomica, Vector3> EstructuraTocada;

    public GameObject ModeloActual => spawnedLung;
    public int PlanosDetectados => planeManager != null && planeManager.enabled ? planeManager.trackables.count : 0;
    public IReadOnlyList<EstructuraAnatomica> Estructuras => estructuras;
    public EstructuraAnatomica Aislada => aislada;
    public CapaAnatomica CapasVisibles => capasVisibles;
    public float Opacidad => opacidad;

    ARRaycastManager raycastManager;
    GameObject spawnedLung;
    Vector3 initialScale;
    Quaternion initialRotation;

    static readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();
    static readonly List<RaycastResult> uiHits = new List<RaycastResult>();
    readonly HashSet<int> toquesSobreUI = new HashSet<int>();
    readonly List<Touch> toques = new List<Touch>();
    readonly List<EstructuraAnatomica> estructuras = new List<EstructuraAnatomica>();

    CapaAnatomica capasVisibles = CapaAnatomica.Todas;
    EstructuraAnatomica aislada;
    float opacidad = 1f;

    void Awake()
    {
        raycastManager = GetComponent<ARRaycastManager>();
        if (planeManager == null) planeManager = GetComponent<ARPlaneManager>();
        if (planeManager == null) planeManager = FindFirstObjectByType<ARPlaneManager>(FindObjectsInactive.Include);
        if (camaraAR == null) camaraAR = Camera.main;
    }

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
#if UNITY_EDITOR
        // Permite probar con el mouse en el editor (XR Simulation).
        TouchSimulation.Enable();
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        TouchSimulation.Disable();
#endif
        EnhancedTouchSupport.Disable();
    }

    void Update()
    {
        // Filtra los toques que empezaron encima de un botón o panel de la interfaz.
        toques.Clear();
        foreach (var t in Touch.activeTouches)
        {
            if (t.phase == TouchPhase.Began && SobreUI(t.screenPosition)) toquesSobreUI.Add(t.touchId);
            bool ignorado = toquesSobreUI.Contains(t.touchId);
            if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) toquesSobreUI.Remove(t.touchId);
            if (!ignorado) toques.Add(t);
        }
        if (toques.Count == 0) return;

        // 1. Si aún no se ha colocado el pulmón, tocar un plano lo instancia
        if (spawnedLung == null)
        {
            if (!permitirColocar || lungPrefab == null) return;
            foreach (var t in toques)
            {
                if (t.phase == TouchPhase.Began &&
                    raycastManager.Raycast(t.screenPosition, hits, TrackableType.PlaneWithinPolygon))
                {
                    // Este mismo dedo no debe contar como "toque para seleccionar" al levantarse.
                    toquesSobreUI.Add(t.touchId);
                    Colocar(hits[0].pose);
                    break;
                }
            }
            return;
        }

        if (!permitirInteraccion) return;

        if (toques.Count == 1)
        {
            var t = toques[0];
            // 2. Un dedo arrastrando: rotar sobre el eje vertical (Y)
            if (t.phase == TouchPhase.Moved)
            {
                spawnedLung.transform.Rotate(0f, -t.delta.x * rotationSpeed, 0f, Space.World);
            }
            // 3. Toque corto: seleccionar estructura
            else if (t.phase == TouchPhase.Ended &&
                     (t.time - t.startTime) <= duracionMaximaToque &&
                     (t.screenPosition - t.startScreenPosition).magnitude <= movimientoMaximoToque)
            {
                IntentarSeleccion(t.screenPosition);
            }
        }
        // 4. Dos dedos pellizcando (Pinch-to-Zoom): escalar proporcionalmente
        else
        {
            var t0 = toques[0];
            var t1 = toques[1];
            Vector2 prev0 = t0.screenPosition - t0.delta;
            Vector2 prev1 = t1.screenPosition - t1.delta;
            float prevMagnitude = (prev0 - prev1).magnitude;
            float currentMagnitude = (t0.screenPosition - t1.screenPosition).magnitude;
            float difference = currentMagnitude - prevMagnitude;

            float currentFactor = spawnedLung.transform.localScale.x / initialScale.x;
            float newFactor = Mathf.Clamp(currentFactor + difference * zoomSpeed, minScaleMultiplier, maxScaleMultiplier);
            spawnedLung.transform.localScale = initialScale * newFactor;
        }
    }

    // ---------------------------------------------------------------- Colocación

    void Colocar(Pose pose)
    {
        if (camaraAR == null) camaraAR = Camera.main;

        // Mira hacia la cámara (solo giro horizontal).
        Quaternion rot = pose.rotation;
        if (camaraAR != null)
        {
            Vector3 dir = camaraAR.transform.position - pose.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-4f) rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
        rot *= Quaternion.Euler(0f, rotacionExtraY, 0f);

        // Un pivote vacío: así el pulmón gira y escala desde su centro, apoyado en la mesa.
        spawnedLung = new GameObject("Pulmon RA");
        spawnedLung.transform.SetPositionAndRotation(pose.position, rot);
        var modelo = Instantiate(lungPrefab, spawnedLung.transform);

        if (ajustarTamano && CalcularBounds(modelo, out Bounds b) && b.size.y > 1e-5f)
            spawnedLung.transform.localScale = Vector3.one * (alturaObjetivo / b.size.y);

        if (CalcularBounds(modelo, out b))
        {
            Vector3 desplazamiento = new Vector3(pose.position.x - b.center.x, pose.position.y - b.min.y, pose.position.z - b.center.z);
            modelo.transform.position += desplazamiento;
        }

        initialScale = spawnedLung.transform.localScale;
        initialRotation = spawnedLung.transform.rotation;

        estructuras.Clear();
        spawnedLung.GetComponentsInChildren(true, estructuras);
        if (estructuras.Count == 0) estructuras.AddRange(EstructuraAnatomica.ConfigurarAutomaticamente(spawnedLung));
        foreach (var e in estructuras) e.Inicializar();

        AplicarEstado();
        MostrarPlanos(false);
        ModeloColocado?.Invoke(spawnedLung);
    }

    static bool CalcularBounds(GameObject go, out Bounds bounds)
    {
        bounds = default;
        bool hay = false;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (!hay) { bounds = r.bounds; hay = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return hay;
    }

    void IntentarSeleccion(Vector2 posicion)
    {
        if (camaraAR == null) camaraAR = Camera.main;
        if (camaraAR == null) return;

        var rayo = camaraAR.ScreenPointToRay(posicion);
        var impactos = Physics.RaycastAll(rayo, 50f);
        Array.Sort(impactos, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in impactos)
        {
            var e = hit.collider.GetComponentInParent<EstructuraAnatomica>();
            if (e != null && e.Visible)
            {
                EstructuraTocada?.Invoke(e, hit.point);
                return;
            }
        }
    }

    static bool SobreUI(Vector2 posicion)
    {
        var es = EventSystem.current;
        if (es == null) return false;
        var datos = new PointerEventData(es) { position = posicion };
        uiHits.Clear();
        es.RaycastAll(datos, uiHits);
        return uiHits.Count > 0;
    }

    // ---------------------------------------------------------------- API para la interfaz

    public void ResetearVista()
    {
        if (spawnedLung == null) return;
        spawnedLung.transform.rotation = initialRotation;
        spawnedLung.transform.localScale = initialScale;
    }

    public void QuitarModelo()
    {
        if (spawnedLung != null) Destroy(spawnedLung);
        spawnedLung = null;
        estructuras.Clear();
        aislada = null;
    }

    /// <summary>
    /// ¿Hay un proveedor XR activo que detecte planos? (ARCore en el celular, XR Simulation en el editor)
    /// </summary>
    public static bool HayDeteccionDePlanos()
    {
        var subsistemas = new List<XRPlaneSubsystem>();
        SubsystemManager.GetSubsystems(subsistemas);
        return subsistemas.Count > 0;
    }

    public void MostrarPlanos(bool visibles)
    {
        if (planeManager == null) return;
        // Evita el error "No active XRPlaneSubsystem" cuando no hay proveedor XR.
        if (visibles && !HayDeteccionDePlanos()) return;
        planeManager.enabled = visibles;
        foreach (var plano in planeManager.trackables)
            plano.gameObject.SetActive(visibles);
    }

    public EstructuraAnatomica BuscarEstructura(TipoEstructura tipo)
    {
        foreach (var e in estructuras)
            if (e != null && e.tipo == tipo) return e;
        return null;
    }

    public void SetCapaVisible(CapaAnatomica capa, bool visible)
    {
        if (visible) capasVisibles |= capa;
        else capasVisibles &= ~capa;
        AplicarEstado();
    }

    /// <summary>1 = opaco, valores menores = más transparente.</summary>
    public void SetOpacidad(float valor)
    {
        opacidad = Mathf.Clamp(valor, 0.1f, 1f);
        AplicarEstado();
    }

    /// <summary>Muestra solo esa estructura. Pasa null para volver a mostrar todas.</summary>
    public void Aislar(EstructuraAnatomica estructura)
    {
        aislada = estructura;
        AplicarEstado();
    }

    public void MostrarTodo()
    {
        capasVisibles = CapaAnatomica.Todas;
        aislada = null;
        opacidad = 1f;
        AplicarEstado();
    }

    void AplicarEstado()
    {
        foreach (var e in estructuras)
        {
            if (e == null) continue;
            bool visible = (e.capas & capasVisibles) != 0 && (aislada == null || aislada == e);
            e.SetVisible(visible);
            e.SetOpacidad(opacidad);
        }
    }
}
