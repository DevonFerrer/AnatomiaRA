using UnityEngine;
using UnityEngine.UI;

public enum AccionUI
{
    IrInicio,
    IniciarExperiencia,
    ComoUsar,
    Acerca,
    Atras,
    PermitirCamara,
    CancelarEscaneo,
    EscanearDeNuevo,
    ResetVista,
    AbrirCapas,
    AlternarEtiquetas,
    ExplorarEstructura,
    MostrarTodo,
    AislarEstructura,
    CerrarInfo,
    Entendido,
    AbrirAjustes,
}

/// <summary>Indica qué hace un botón. AnatomiaUIManager lo conecta al iniciar.</summary>
[RequireComponent(typeof(Button))]
public class BotonAccion : MonoBehaviour
{
    public AccionUI accion;
}
