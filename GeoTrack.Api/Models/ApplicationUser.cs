using Microsoft.AspNetCore.Identity;

namespace GeoTrack.Api.Models
{
    /// <summary>
    /// GEO-18 : utilisateur de GeoTrack.
    /// Herite d'IdentityUser (Id, UserName, Email, PasswordHash, verrouillage...)
    /// et ajoute les champs propres a la gestion de flotte.
    /// </summary>
    public class ApplicationUser : IdentityUser
    {
        /// <summary>Nom affiche dans l'en-tete de l'application web.</summary>
        public string NomComplet { get; set; } = string.Empty;

        /// <summary>Date de creation du compte (UTC).</summary>
        public DateTime DateCreation { get; set; } = DateTime.UtcNow;
    }
}
