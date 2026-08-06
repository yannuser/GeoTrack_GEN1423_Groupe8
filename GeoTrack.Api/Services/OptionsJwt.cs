namespace GeoTrack.Api.Services
{
    /// <summary>
    /// GEO-18 : parametres de signature et de validation des jetons JWT.
    /// Alimente depuis la section "Jwt" de la configuration.
    /// </summary>
    public class OptionsJwt
    {
        public const string Section = "Jwt";

        /// <summary>
        /// Cle de signature HMAC-SHA256. Doit faire au moins 32 caracteres.
        /// En production, la fournir par variable d'environnement ou user-secrets
        /// (Jwt__Cle), jamais en clair dans appsettings.json.
        /// </summary>
        public string Cle { get; set; } = string.Empty;

        public string Emetteur { get; set; } = "GeoTrack.Api";

        public string Audience { get; set; } = "GeoTrack.Web";

        /// <summary>Duree de validite du jeton, en minutes (GEO-18 : 60).</summary>
        public int DureeMinutes { get; set; } = 60;
    }
}
