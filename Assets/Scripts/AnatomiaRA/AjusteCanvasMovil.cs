using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Mantiene el diseño de Figma (390 x 844) en cualquier celular:
/// en pantallas más alargadas ajusta por ancho; en más anchas, por alto (la columna queda centrada).
/// </summary>
[RequireComponent(typeof(CanvasScaler))]
public class AjusteCanvasMovil : MonoBehaviour
{
    public Vector2 referencia = new Vector2(390f, 844f);

    CanvasScaler scaler;
    int ancho, alto;

    void Awake()
    {
        scaler = GetComponent<CanvasScaler>();
        Aplicar();
    }

    void Update()
    {
        if (Screen.width != ancho || Screen.height != alto) Aplicar();
    }

    void Aplicar()
    {
        ancho = Screen.width;
        alto = Screen.height;
        if (ancho <= 0 || alto <= 0) return;

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referencia;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        float aspecto = (float)alto / ancho;
        scaler.matchWidthOrHeight = aspecto >= referencia.y / referencia.x ? 0f : 1f;
    }
}
