namespace GeoTrack.Api.Models
{
    /// <summary>
    /// GEO-15 : conducteur affectable a un vehicule.
    ///
    /// Cette entite n'est pas decrite explicitement par le contrat de GEO-15 :
    /// <c>IConducteurRepository.GetDisponiblesAsync()</c> ne renvoie qu'un
    /// <c>(int Id, string Nom)</c>. Elle porte donc le strict minimum permettant
    /// de satisfaire ce contrat.
    ///
    /// La disponibilite n'est volontairement PAS un champ stocke : un conducteur
    /// est considere disponible s'il n'est reference par aucun vehicule via
    /// <c>Vehicule.ConducteurId</c> (voir ConducteurRepository). Aucun drapeau a
    /// maintenir a la main, donc aucun risque de desynchronisation.
    /// </summary>
    public class Conducteur
    {
        public int Id { get; set; }

        public string Nom { get; set; } = string.Empty;
    }
}
