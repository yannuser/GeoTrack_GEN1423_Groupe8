using GeoTrack.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace GeoTrack.Api.Services
{
    /// <summary>
    /// Parametres du compte de developpement, section "SeedDeveloppement".
    /// Les valeurs par defaut ci-dessous ne servent qu'a eviter un demarrage
    /// bancal si la section est absente d'appsettings.Development.json.
    /// </summary>
    public class OptionsSeedDeveloppement
    {
        public const string Section = "SeedDeveloppement";

        public string Identifiant { get; set; } = "admin";
        public string Courriel { get; set; } = "admin@geotrack.local";
        public string NomComplet { get; set; } = "Administrateur GeoTrack";

        /// <summary>Respecte la politique GEO-18 : 8+ caracteres, majuscule, chiffre.</summary>
        public string MotDePasse { get; set; } = "Admin1234";
    }

    /// <summary>
    /// ############################################################
    /// #  COMPTE DE DEVELOPPEMENT LOCAL UNIQUEMENT                #
    /// ############################################################
    ///
    /// Cree un compte administrateur au demarrage pour qu'un poste de
    /// developpement dispose d'identifiants utilisables immediatement apres
    /// la migration (la table AspNetUsers est vide, et /api/auth/register
    /// exige d'etre deja authentifie : sans ce compte, personne ne peut entrer).
    ///
    /// CE COMPTE NE DOIT JAMAIS EXISTER EN PRODUCTION :
    ///  - son mot de passe est en clair dans appsettings.Development.json,
    ///    fichier versionne dans le depot ;
    ///  - il est identique sur tous les postes de l'equipe.
    ///
    /// La garde est posee dans <see cref="SemerSiDeveloppementAsync"/> : le
    /// semis est ignore des que l'environnement n'est pas Development.
    /// En production, creez le premier compte manuellement en base, puis
    /// utilisez /api/auth/register pour les suivants.
    /// </summary>
    public static class SemeurDeveloppement
    {
        /// <summary>
        /// Point d'entree appele depuis Program.cs.
        /// Ne fait strictement rien hors environnement Development.
        /// </summary>
        public static async Task SemerSiDeveloppementAsync(WebApplication app)
        {
            var journal = app.Services
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(nameof(SemeurDeveloppement));

            // GARDE-FOU : verification explicite de l'environnement.
            if (!app.Environment.IsDevelopment())
            {
                journal.LogInformation(
                    "Semis ignore : environnement {Environnement} (reserve a Development).",
                    app.Environment.EnvironmentName);
                return;
            }

            var options = app.Configuration
                .GetSection(OptionsSeedDeveloppement.Section)
                .Get<OptionsSeedDeveloppement>() ?? new OptionsSeedDeveloppement();

            using var portee = app.Services.CreateScope();
            var gestionnaire = portee.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();

            try
            {
                await SemerCompteAdministrateurAsync(gestionnaire, options, journal);
            }
            catch (Exception cause)
            {
                // Base injoignable ou migration non appliquee : on n'empeche pas
                // l'API de demarrer pour autant, mais on le dit clairement.
                journal.LogError(
                    cause,
                    "Semis du compte de developpement impossible. La base est-elle demarree "
                        + "et la migration appliquee (dotnet ef database update) ?");
            }
        }

        /// <summary>
        /// Coeur du semis, isole de l'hote pour rester testable.
        /// Idempotent : ne fait rien si le compte existe deja.
        /// </summary>
        /// <returns>Vrai si un compte a ete cree lors de cet appel.</returns>
        public static async Task<bool> SemerCompteAdministrateurAsync(
            UserManager<ApplicationUser> gestionnaire,
            OptionsSeedDeveloppement options,
            ILogger journal)
        {
            var existant = await gestionnaire.FindByNameAsync(options.Identifiant)
                           ?? await gestionnaire.FindByEmailAsync(options.Courriel);

            if (existant is not null)
            {
                journal.LogInformation(
                    "Compte de developpement '{Identifiant}' deja present : rien a faire.",
                    options.Identifiant);
                return false;
            }

            var utilisateur = new ApplicationUser
            {
                UserName = options.Identifiant,
                Email = options.Courriel,
                EmailConfirmed = true,
                NomComplet = options.NomComplet
            };

            var resultat = await gestionnaire.CreateAsync(utilisateur, options.MotDePasse);

            if (!resultat.Succeeded)
            {
                // Typiquement un mot de passe de seed non conforme a la politique.
                journal.LogError(
                    "Creation du compte de developpement '{Identifiant}' echouee : {Erreurs}",
                    options.Identifiant,
                    string.Join(", ", resultat.Errors.Select(e => e.Code)));
                return false;
            }

            journal.LogWarning(
                "COMPTE DE DEVELOPPEMENT cree : '{Identifiant}'. "
                    + "Mot de passe par defaut, ne jamais deployer tel quel.",
                options.Identifiant);

            return true;
        }
    }
}
