using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[Flags]
public enum CapaAnatomica
{
    Ninguna = 0,
    Traquea = 1 << 0,
    Bronquios = 1 << 1,
    Lobulos = 1 << 2,
    Segmentos = 1 << 3,
    Todas = Traquea | Bronquios | Lobulos | Segmentos,
}

public enum TipoEstructura
{
    ViaAerea,       // tráquea + bronquios
    Pulmones,       // lóbulos (se detecta cuál por la zona tocada)
    Personalizada,  // usa siempre 'idInfo'
}

/// <summary>
/// Marca una parte del modelo 3D como estructura anatómica: se puede tocar,
/// ocultar por capas, aislar y volver transparente.
/// Si separas el modelo en más piezas (en Blender), pon este componente en cada una
/// con tipo "Personalizada" y el id de su ficha en DatosAnatomia.
/// </summary>
[DisallowMultipleComponent]
public class EstructuraAnatomica : MonoBehaviour
{
    public TipoEstructura tipo = TipoEstructura.Personalizada;

    [Tooltip("Id de la ficha en DatosAnatomia (solo para tipo Personalizada).")]
    public string idInfo = "pulmon";

    [Tooltip("En qué capas del panel 'Capas anatómicas' aparece esta pieza.")]
    public CapaAnatomica capas = CapaAnatomica.Lobulos;

    [Tooltip("Actívalo si al tocar el lado derecho del pulmón sale 'izquierdo' (y viceversa).")]
    public bool invertirLados = false;

    [Tooltip("Copias transparentes de los materiales (las crea el menú Anatomía RA).")]
    public Material[] materialesTransparentes;

    Renderer[] renderers;
    Collider[] colliders;
    Material[][] originales;
    Material[][] transparentes;
    bool usandoTransparente;

    public bool Visible { get; private set; } = true;

    void Awake() => Inicializar();

    public void Inicializar()
    {
        if (renderers != null) return;
        renderers = GetComponentsInChildren<Renderer>(true);
        originales = new Material[renderers.Length][];
        for (int i = 0; i < renderers.Length; i++) originales[i] = renderers[i].sharedMaterials;

        colliders = GetComponentsInChildren<Collider>(true);
        if (colliders.Length == 0)
        {
            // Respaldo: caja que envuelve la malla (el menú Anatomía RA pone MeshCollider, que es más preciso).
            foreach (var r in renderers) r.gameObject.AddComponent<BoxCollider>();
            colliders = GetComponentsInChildren<Collider>(true);
        }
    }

    // ---------------------------------------------------------------- Posiciones

    /// <summary>Punto del modelo en coordenadas normalizadas (0..1) dentro de su caja.</summary>
    public Vector3 PuntoNormalizado(float nx, float ny, float nz)
    {
        Inicializar();
        if (renderers.Length == 0) return transform.position;
        var r = renderers[0];
        var b = r.localBounds;
        var local = new Vector3(Mathf.Lerp(b.min.x, b.max.x, nx), Mathf.Lerp(b.min.y, b.max.y, ny), Mathf.Lerp(b.min.z, b.max.z, nz));
        return r.transform.TransformPoint(local);
    }

    public Vector3 Normalizar(Vector3 puntoMundo)
    {
        Inicializar();
        if (renderers.Length == 0) return Vector3.one * 0.5f;
        var r = renderers[0];
        var b = r.localBounds;
        var p = r.transform.InverseTransformPoint(puntoMundo);
        return new Vector3(Mathf.InverseLerp(b.min.x, b.max.x, p.x), Mathf.InverseLerp(b.min.y, b.max.y, p.y), Mathf.InverseLerp(b.min.z, b.max.z, p.z));
    }

    /// <summary>X normalizada del lado DERECHO del paciente.</summary>
    public float XLadoDerecho => invertirLados ? 0.75f : 0.25f;

    bool EsLadoDerecho(float nx) => invertirLados ? nx > 0.5f : nx < 0.5f;

    /// <summary>Qué ficha mostrar según dónde se tocó.</summary>
    public string IdInfoEnPunto(Vector3 puntoMundo)
    {
        if (tipo == TipoEstructura.Personalizada) return idInfo;
        var n = Normalizar(puntoMundo);

        if (tipo == TipoEstructura.ViaAerea)
            return n.y > 0.55f ? "traquea" : "bronquios";

        // Pulmones: aproximación por zonas (derecho: 3 lóbulos, izquierdo: 2).
        if (EsLadoDerecho(n.x))
        {
            if (n.y > 0.62f) return "lobulo_superior_derecho";
            if (n.y > 0.38f) return "lobulo_medio_derecho";
            return "lobulo_inferior_derecho";
        }
        return n.y > 0.5f ? "lobulo_superior_izquierdo" : "lobulo_inferior_izquierdo";
    }

    // ---------------------------------------------------------------- Visibilidad y transparencia

    public void SetVisible(bool visible)
    {
        Inicializar();
        Visible = visible;
        foreach (var r in renderers) if (r) r.enabled = visible;
        foreach (var c in colliders) if (c) c.enabled = visible;
    }

    public void SetOpacidad(float alfa)
    {
        Inicializar();
        if (alfa >= 0.999f)
        {
            if (usandoTransparente)
            {
                for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterials = originales[i];
                usandoTransparente = false;
            }
            return;
        }

        if (transparentes == null) CrearTransparentes();
        if (!usandoTransparente)
        {
            for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterials = transparentes[i];
            usandoTransparente = true;
        }
        foreach (var arr in transparentes)
            foreach (var m in arr) PonerAlfa(m, alfa);
    }

    void CrearTransparentes()
    {
        transparentes = new Material[renderers.Length][];
        int k = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            var orig = originales[i];
            var arr = new Material[orig.Length];
            for (int j = 0; j < orig.Length; j++, k++)
            {
                Material plantilla = materialesTransparentes != null && k < materialesTransparentes.Length ? materialesTransparentes[k] : null;
                arr[j] = plantilla != null ? new Material(plantilla) : HacerTransparente(new Material(orig[j]));
            }
            transparentes[i] = arr;
        }
    }

    static void PonerAlfa(Material m, float alfa)
    {
        if (m == null) return;
        if (m.HasProperty("_BaseColor")) { var c = m.GetColor("_BaseColor"); c.a = alfa; m.SetColor("_BaseColor", c); }
        else if (m.HasProperty("_Color")) { var c = m.GetColor("_Color"); c.a = alfa; m.SetColor("_Color", c); }
    }

    /// <summary>Configura un material URP Lit como transparente (alpha blend).</summary>
    public static Material HacerTransparente(Material m)
    {
        if (m == null) return null;
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.DisableKeyword("_ALPHATEST_ON");
        m.SetShaderPassEnabled("DepthOnly", false);
        m.renderQueue = (int)RenderQueue.Transparent;
        return m;
    }

    void OnDestroy()
    {
        if (transparentes == null) return;
        foreach (var arr in transparentes)
            foreach (var m in arr) if (m) Destroy(m);
    }

    // ---------------------------------------------------------------- Configuración automática

    /// <summary>
    /// Pone EstructuraAnatomica en cada malla del modelo. Con 2 mallas (tu caso):
    /// la que está más arriba es la vía aérea (tráquea + bronquios) y la otra los pulmones.
    /// </summary>
    public static List<EstructuraAnatomica> ConfigurarAutomaticamente(GameObject raiz)
    {
        var lista = new List<EstructuraAnatomica>();
        var rs = raiz.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return lista;

        int iAlta = 0;
        for (int i = 1; i < rs.Length; i++)
            if (CentroY(rs[i]) > CentroY(rs[iAlta])) iAlta = i;

        bool hayPulmones = false;
        for (int i = 0; i < rs.Length; i++)
        {
            var e = rs[i].GetComponent<EstructuraAnatomica>();
            if (e == null) e = rs[i].gameObject.AddComponent<EstructuraAnatomica>();

            if (rs.Length == 1) { e.tipo = TipoEstructura.Pulmones; e.capas = CapaAnatomica.Todas; }
            else if (i == iAlta) { e.tipo = TipoEstructura.ViaAerea; e.capas = CapaAnatomica.Traquea | CapaAnatomica.Bronquios; }
            else if (!hayPulmones) { e.tipo = TipoEstructura.Pulmones; e.capas = CapaAnatomica.Lobulos | CapaAnatomica.Segmentos; hayPulmones = true; }
            else { e.tipo = TipoEstructura.Personalizada; e.idInfo = "pulmon"; e.capas = CapaAnatomica.Lobulos; }
            lista.Add(e);
        }
        return lista;
    }

    static float CentroY(Renderer r)
    {
        var mf = r.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null) return r.transform.TransformPoint(mf.sharedMesh.bounds.center).y;
        return r.bounds.center.y;
    }
}
