using System.Collections.Generic;

public struct FichaAnatomica
{
    public string titulo;
    public string descripcion;
    public string ubicacion;

    public FichaAnatomica(string titulo, string descripcion, string ubicacion)
    {
        this.titulo = titulo;
        this.descripcion = descripcion;
        this.ubicacion = ubicacion;
    }
}

/// <summary>Textos de la pantalla "Información de estructura". Edítalos aquí.</summary>
public static class DatosAnatomia
{
    static readonly Dictionary<string, FichaAnatomica> fichas = new Dictionary<string, FichaAnatomica>
    {
        ["traquea"] = new FichaAnatomica(
            "Tráquea",
            "Es un conducto respiratorio formado por anillos cartilaginosos que conecta la laringe con los bronquios, permitiendo el paso del aire hacia los pulmones.",
            "Va desde la laringe (a nivel de C6) hasta la carina (T4–T5), por delante del esófago, donde se divide en los dos bronquios principales."),

        ["bronquios"] = new FichaAnatomica(
            "Bronquios principales",
            "Nacen de la bifurcación de la tráquea y llevan el aire a cada pulmón. El bronquio derecho es más corto, ancho y vertical que el izquierdo.",
            "Entran a cada pulmón por el hilio pulmonar, junto con las arterias y venas pulmonares, y se dividen en bronquios lobulares."),

        ["lobulo_superior_derecho"] = new FichaAnatomica(
            "Lóbulo superior derecho",
            "Se encuentra en la parte superior del pulmón derecho y participa en el intercambio de oxígeno y dióxido de carbono.",
            "Está separado del lóbulo medio por la fisura horizontal y del lóbulo inferior por la fisura oblicua. Su vértice sube por encima de la clavícula."),

        ["lobulo_medio_derecho"] = new FichaAnatomica(
            "Lóbulo medio derecho",
            "Es el lóbulo más pequeño y solo existe en el pulmón derecho. Recibe el aire del bronquio lobar medio.",
            "Se ubica entre la fisura horizontal y la fisura oblicua, en la parte anterior del pulmón derecho, junto al corazón."),

        ["lobulo_inferior_derecho"] = new FichaAnatomica(
            "Lóbulo inferior derecho",
            "Es el lóbulo más grande del pulmón derecho y ocupa su porción inferior y posterior.",
            "Queda por debajo de la fisura oblicua y descansa sobre la cúpula derecha del diafragma, que lo separa del hígado."),

        ["lobulo_superior_izquierdo"] = new FichaAnatomica(
            "Lóbulo superior izquierdo",
            "Ocupa la parte superior y anterior del pulmón izquierdo. Incluye la língula, equivalente al lóbulo medio del lado derecho.",
            "Está por encima de la fisura oblicua; su borde anterior presenta la escotadura cardíaca, donde se apoya el corazón."),

        ["lobulo_inferior_izquierdo"] = new FichaAnatomica(
            "Lóbulo inferior izquierdo",
            "Se encuentra en la base del pulmón izquierdo y descansa sobre el diafragma. Es una estructura fundamental para la expansión pulmonar y el intercambio de gases.",
            "Queda por debajo de la fisura oblicua, sobre la cúpula izquierda del diafragma, en relación con el bazo y el estómago."),

        ["pulmon"] = new FichaAnatomica(
            "Pulmón",
            "Órgano principal de la respiración, donde ocurre el intercambio de oxígeno y dióxido de carbono entre el aire y la sangre.",
            "Los dos pulmones ocupan la cavidad torácica a ambos lados del corazón, protegidos por las costillas y envueltos por la pleura."),
    };

    public static FichaAnatomica Obtener(string id)
    {
        if (id != null && fichas.TryGetValue(id, out var ficha)) return ficha;
        return fichas["pulmon"];
    }

    /// <summary>A qué pieza del modelo pertenece cada ficha (para "Aislar estructura").</summary>
    public static TipoEstructura TipoParaId(string id)
    {
        return id == "traquea" || id == "bronquios" ? TipoEstructura.ViaAerea : TipoEstructura.Pulmones;
    }
}
