using GeoTrack.Api.Models;
using GeoTrack.Api.Models.Auth;
using GeoTrack.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GeoTrack.Api.Controllers
{
    /// <summary>
    /// GEO-18 : authentification par jeton JWT adossee a ASP.NET Core Identity.
    /// </summary>
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        /// <summary>
        /// Message unique pour tous les echecs de connexion.
        /// Ne jamais distinguer "compte inconnu", "mot de passe faux" ou
        /// "compte verrouille" : cela permettrait d'enumerer les comptes valides.
        /// </summary>
        public const string MessageEchecGenerique = "Identifiant ou mot de passe incorrect";

        private readonly UserManager<ApplicationUser> _gestionnaireUtilisateurs;
        private readonly SignInManager<ApplicationUser> _gestionnaireConnexion;
        private readonly IJetonJwtService _jetons;

        public AuthController(
            UserManager<ApplicationUser> gestionnaireUtilisateurs,
            SignInManager<ApplicationUser> gestionnaireConnexion,
            IJetonJwtService jetons)
        {
            _gestionnaireUtilisateurs = gestionnaireUtilisateurs;
            _gestionnaireConnexion = gestionnaireConnexion;
            _jetons = jetons;
        }

        // POST api/auth/login
        [HttpPost("login")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ReponseConnexion), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ReponseErreurAuth), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Connexion([FromBody] RequeteConnexion requete)
        {
            if (requete is null
                || string.IsNullOrWhiteSpace(requete.Identifiant)
                || string.IsNullOrWhiteSpace(requete.MotDePasse))
            {
                return Echec();
            }

            // L'identifiant accepte indifferemment le nom d'utilisateur ou le courriel.
            var utilisateur = await _gestionnaireUtilisateurs.FindByNameAsync(requete.Identifiant)
                              ?? await _gestionnaireUtilisateurs.FindByEmailAsync(requete.Identifiant);

            if (utilisateur is null)
            {
                return Echec();
            }

            // lockoutOnFailure: true -> incremente le compteur d'echecs et
            // declenche le verrouillage configure dans Program.cs (5 essais / 5 min).
            // CheckPasswordSignInAsync ne pose aucun cookie : adapte a une API JWT.
            var resultat = await _gestionnaireConnexion.CheckPasswordSignInAsync(
                utilisateur, requete.MotDePasse, lockoutOnFailure: true);

            // Verrouillage, mot de passe errone, compte non autorise : meme reponse.
            if (!resultat.Succeeded)
            {
                return Echec();
            }

            var (jeton, expiration) = _jetons.Generer(utilisateur);

            return Ok(new ReponseConnexion
            {
                Jeton = jeton,
                Expiration = expiration,
                Identifiant = utilisateur.UserName ?? string.Empty,
                NomComplet = utilisateur.NomComplet
            });
        }

        // POST api/auth/register
        //
        // Protege : seul un utilisateur deja authentifie peut creer un compte.
        // Il n'existe donc aucune inscription libre — le premier compte vient du
        // semeur de developpement (SemeurDeveloppement) ou, en production, d'une
        // creation manuelle en base.
        [HttpPost("register")]
        [Authorize]
        [ProducesResponseType(typeof(ReponseInscription), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ReponseErreurInscription), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ReponseErreurInscription), StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Inscription([FromBody] RequeteInscription requete)
        {
            if (requete is null)
            {
                return BadRequest(new ReponseErreurInscription
                {
                    Message = "Le corps de la requete est vide ou illisible."
                });
            }

            var champsManquants = new List<string>();
            if (string.IsNullOrWhiteSpace(requete.Identifiant)) champsManquants.Add("Identifiant");
            if (string.IsNullOrWhiteSpace(requete.Courriel)) champsManquants.Add("Courriel");
            if (string.IsNullOrWhiteSpace(requete.MotDePasse)) champsManquants.Add("MotDePasse");

            if (champsManquants.Count > 0)
            {
                return BadRequest(new ReponseErreurInscription
                {
                    Message = "Champs obligatoires manquants.",
                    Erreurs = champsManquants
                        .Select(champ => $"Le champ {champ} est obligatoire.")
                        .ToList()
                });
            }

            var identifiant = requete.Identifiant.Trim();
            var courriel = requete.Courriel.Trim();

            // Verification explicite avant CreateAsync : permet un message cible
            // ("identifiant" vs "courriel") la ou Identity renverrait un code brut.
            if (await _gestionnaireUtilisateurs.FindByNameAsync(identifiant) is not null)
            {
                return Conflict(new ReponseErreurInscription
                {
                    Message = $"L'identifiant '{identifiant}' est deja utilise.",
                    Erreurs = { "DuplicateUserName" }
                });
            }

            if (await _gestionnaireUtilisateurs.FindByEmailAsync(courriel) is not null)
            {
                return Conflict(new ReponseErreurInscription
                {
                    Message = $"Le courriel '{courriel}' est deja utilise.",
                    Erreurs = { "DuplicateEmail" }
                });
            }

            var nouveau = new ApplicationUser
            {
                UserName = identifiant,
                Email = courriel,
                EmailConfirmed = true,
                NomComplet = string.IsNullOrWhiteSpace(requete.NomComplet)
                    ? identifiant
                    : requete.NomComplet.Trim()
            };

            // CreateAsync applique la politique de mot de passe configuree dans
            // Program.cs (8 caracteres, majuscule, chiffre) : aucune regle dupliquee ici.
            var resultat = await _gestionnaireUtilisateurs.CreateAsync(nouveau, requete.MotDePasse);

            if (!resultat.Succeeded)
            {
                var erreurs = resultat.Errors.Select(e => e.Description).ToList();

                // Doublon detecte par Identity malgre les controles ci-dessus
                // (creation concurrente) : c'est un conflit, pas une saisie invalide.
                var estDoublon = resultat.Errors.Any(e => e.Code.StartsWith("Duplicate"));

                var corps = new ReponseErreurInscription
                {
                    Message = estDoublon
                        ? "Cet identifiant ou ce courriel est deja utilise."
                        : "Le mot de passe ne respecte pas les regles de securite.",
                    Erreurs = erreurs
                };

                return estDoublon ? Conflict(corps) : BadRequest(corps);
            }

            var reponse = new ReponseInscription
            {
                Id = nouveau.Id,
                Identifiant = nouveau.UserName!,
                Courriel = nouveau.Email!,
                NomComplet = nouveau.NomComplet
            };

            return CreatedAtAction(nameof(Inscription), new { id = nouveau.Id }, reponse);
        }

        private UnauthorizedObjectResult Echec() =>
            Unauthorized(new ReponseErreurAuth { Message = MessageEchecGenerique });
    }
}
