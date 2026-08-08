namespace GeoTrack.Api.Models
{
    /// <summary>
    /// GEO-58 : origine d'une alerte consignee dans la table centralisee.
    ///
    /// A ne pas confondre avec <see cref="TypeAlerteZone"/>, qui qualifie la
    /// regle portee par une zone geographique (ce que la zone surveille). Celui-ci
    /// qualifie l'alerte effectivement survenue, toutes sources confondues.
    /// </summary>
    public enum TypeAlerte
    {
        VitesseExcessive = 0,
        SortieZone = 1
    }
}
