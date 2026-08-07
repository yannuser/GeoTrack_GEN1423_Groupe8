namespace GeoTrack.Api.Models
{
    /// <summary>
    /// GEO-9 : nature de l'alerte declenchee par une zone geographique.
    ///
    /// Une seule valeur pour l'instant. L'enum existe des maintenant pour que
    /// l'ajout d'EntreeZone, ou d'un depassement de duree de stationnement,
    /// n'impose pas de migration de schema : la colonne est deja typee.
    ///
    /// Volontairement distinct d'un futur TypeAlerte global : celui-ci ne
    /// qualifie que les alertes issues du geofencing.
    /// </summary>
    public enum TypeAlerteZone
    {
        SortieZone = 0
    }
}
