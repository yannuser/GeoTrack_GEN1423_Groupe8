namespace GeoTrack.Api.Models.Auth
{
    /// <summary>
    /// Corps attendu par POST /api/auth/login.
    ///
    /// Volontairement sans [Required] : la validation automatique d'[ApiController]
    /// renverrait un 400 nommant le champ manquant, ce qui distinguerait un
    /// identifiant vide d'un mot de passe vide. Les champs absents sont traites
    /// par AuthController comme n'importe quel autre echec -> 401 generique.
    /// </summary>
    public class RequeteConnexion
    {
        public string Identifiant { get; set; } = string.Empty;

        public string MotDePasse { get; set; } = string.Empty;
    }

    /// <summary>Reponse renvoyee en cas de connexion reussie.</summary>
    public class ReponseConnexion
    {
        public string Jeton { get; set; } = string.Empty;

        /// <summary>Expiration du jeton (UTC), pour que le client anticipe le renouvellement.</summary>
        public DateTime Expiration { get; set; }

        public string Identifiant { get; set; } = string.Empty;

        public string NomComplet { get; set; } = string.Empty;
    }

    /// <summary>
    /// Reponse d'echec. Le message reste volontairement identique quel que soit
    /// le motif reel (compte inconnu, mot de passe faux, compte verrouille) :
    /// preciser lequel des deux est errone permettrait d'enumerer les comptes.
    /// </summary>
    public class ReponseErreurAuth
    {
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Corps attendu par POST /api/auth/register.
    ///
    /// Comme pour la connexion, pas de [Required] : AuthController valide les
    /// champs lui-meme afin de renvoyer un corps de reponse homogene
    /// (<see cref="ReponseErreurInscription"/>) plutot que le ValidationProblemDetails
    /// genere automatiquement par [ApiController].
    /// </summary>
    public class RequeteInscription
    {
        public string Identifiant { get; set; } = string.Empty;

        public string Courriel { get; set; } = string.Empty;

        public string MotDePasse { get; set; } = string.Empty;

        public string NomComplet { get; set; } = string.Empty;
    }

    /// <summary>Reponse renvoyee apres creation reussie d'un compte.</summary>
    public class ReponseInscription
    {
        public string Id { get; set; } = string.Empty;

        public string Identifiant { get; set; } = string.Empty;

        public string Courriel { get; set; } = string.Empty;

        public string NomComplet { get; set; } = string.Empty;
    }

    /// <summary>
    /// Erreur d'inscription. Contrairement a la connexion, on est ici explicite :
    /// l'appelant est deja authentifie, il n'y a donc pas de risque d'enumeration
    /// de comptes, et il a besoin de savoir ce qu'il doit corriger.
    /// </summary>
    public class ReponseErreurInscription
    {
        public string Message { get; set; } = string.Empty;

        /// <summary>Details, un par regle enfreinte (codes Identity traduits).</summary>
        public List<string> Erreurs { get; set; } = new();
    }
}
