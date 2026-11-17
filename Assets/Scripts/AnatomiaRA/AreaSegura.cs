using UnityEngine;

/// <summary>Ajusta el contenido al área segura de la pantalla (muesca, barra de estado, gestos).</summary>
[RequireComponent(typeof(RectTransform))]
public class AreaSegura : MonoBehaviour
{
    RectTransform rt;
    Rect ultima;
    int ancho, alto;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        Aplicar();
    }

    void Update()
    {
        if (Screen.safeArea != ultima || Screen.width != ancho || Screen.height != alto) Aplicar();
    }

    void Aplicar()
    {
        ultima = Screen.safeArea;
        ancho = Screen.width;
        alto = Screen.height;
        if (ancho <= 0 || alto <= 0) return;

        Vector2 min = ultima.position;
        Vector2 max = ultima.position + ultima.size;
        min.x /= ancho; min.y /= alto;
        max.x /= ancho; max.y /= alto;
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
