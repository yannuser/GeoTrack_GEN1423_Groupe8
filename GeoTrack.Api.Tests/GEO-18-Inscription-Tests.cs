using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GeoTrack.Api.Models;
using GeoTrack.Api.Models.Auth;
using GeoTrack.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;

namespace GeoTrack.Api.Tests
{
    // =======================================================================
    // POST /api/auth/register : protection d'acces
    // =======================================================================
    public class GEO18_InscriptionProtectionTests
    {
        private static object CompteValide() => new
        {
            Identifiant = "marie.tremblay",
            Courriel = "marie.tremblay@geotrack.test",
            MotDePasse = "MotDePasse2",
            NomComplet = "Marie Tremblay"
        };

        [Fact]
        public async Task Register_SansJeton_Retourne401()
        {
            await using var fabrique = new FabriqueApiTest();
            var client = fabrique.CreerClient();

            var reponse = await client.PostAsJsonAsync("/api/auth/register", CompteValide());

            Assert.Equal(HttpStatusCode.Unauthorized, reponse.StatusCode);

            // Aucun compte ne doit avoir ete cree malgre la tentative.
            Assert.Null(await fabrique.TrouverUtilisateurAsync("marie.tremblay"));
        }

        [Fact]
        public async Task Register_AvecJetonInvalide_Retourne401()
        {
            await using var fabrique = new FabriqueApiTest();
            var client = fabrique.CreerClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", "ceci.nest.pas.un.jeton");

            var reponse = await client.PostAsJsonAsync("/api/auth/register", CompteValide());

            Assert.Equal(HttpStatusCode.Unauthorized, reponse.StatusCode);
            Assert.Null(await fabrique.TrouverUtilisateurAsync("marie.tremblay"));
        }

        [Fact]
        public async Task Register_AvecJetonValide_NEstPasRefuse()
        {
            await using var fabrique = new FabriqueApiTest();
            var client = await fabrique.CreerClientAuthentifieAsync();

            var reponse = await client.PostAsJsonAsync("/api/auth/register", CompteValide());

            Assert.NotEqual(HttpStatusCode.Unauthorized, reponse.StatusCode);
        }
    }

    // =======================================================================
    // POST /api/auth/register : creation de compte
    // =======================================================================
    public class GEO18_InscriptionCreationTests
    {
        [Fact]
        public async Task Register_CreeLeCompte_EtRetourne201()
        {
            await using var fabrique = new FabriqueApiTest();
            var client = await fabrique.CreerClientAuthentifieAsync();

            var reponse = await client.PostAsJsonAsync("/api/auth/register", new
            {
                Identifiant = "marie.tremblay",
                Courriel = "marie.tremblay@geotrack.test",
                MotDePasse = "MotDePasse2",
                NomComplet = "Marie Tremblay"
            });

            Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);

            var corps = await reponse.Content.ReadFromJsonAsync<ReponseInscription>();
            Assert.NotNull(corps);
            Assert.Equal("marie.tremblay", corps!.Identifiant);
            Assert.Equal("marie.tremblay@geotrack.test", corps.Courriel);
            Assert.Equal("Marie Tremblay", corps.NomComplet);
            Assert.False(string.IsNullOrWhiteSpace(corps.Id));

            // Le compte existe reellement en base.
            var cree = await fabrique.TrouverUtilisateurAsync("marie.tremblay");
            Assert.NotNull(cree);
            Assert.Equal("Marie Tremblay", cree!.NomComplet);
        }

        [Fact]
        public async Task Register_NeRenvoieJamaisLeMotDePasse()
        {
            await using var fabrique = new FabriqueApiTest();
            var client = await fabrique.CreerClientAuthentifieAsync();

            var reponse = await client.PostAsJsonAsync("/api/auth/register", new
            {
                Identifiant = "marie.tremblay",
                Courriel = "marie.tremblay@geotrack.test",
                MotDePasse = "MotDePasse2",
                NomComplet = "Marie Tremblay"
            });

            var texte = await reponse.Content.ReadAsStringAsync();
            Assert.DoesNotContain("MotDePasse2", texte);
            Assert.DoesNotContain("PasswordHash", texte, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Register_LeNouveauCompte_PeutSeConnecter()
        {
            await using var fabrique = new FabriqueApiTest();
            var client = await fabrique.CreerClientAuthentifieAsync();

            await client.PostAsJsonAsync("/api/auth/register", new
            {
                Identifiant = "marie.tremblay",
                Courriel = "marie.tremblay@geotrack.test",
                MotDePasse = "MotDePasse2",
                NomComplet = "Marie Tremblay"
            });

            // Nouveau client anonyme : on verifie le parcours complet du compte cree.
            var clientNeuf = fabrique.CreerClient();
            var connexion = await clientNeuf.PostAsJsonAsync("/api/auth/login", new
            {
                Identifiant = "marie.tremblay",
                MotDePasse = "MotDePasse2"
            });

            Assert.Equal(HttpStatusCode.OK, connexion.StatusCode);

            var session = await connexion.Content.ReadFromJsonAsync<ReponseConnexion>();
            Assert.False(string.IsNullOrWhiteSpace(session!.Jeton));

            // Et ce jeton ouvre bien l'acces aux positions protegees.
            clientNeuf.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", session.Jeton);
            var positions = await clientNeuf.GetAsync("/api/positionsgps");

            Assert.Equal(HttpStatusCode.OK, positions.StatusCode);
        }

        [Fact]
        public async Task Register_SansNomComplet_RetombeSurLIdentifiant()
        {
            await using var fabrique = new FabriqueApiTest();
            var client = await fabrique.CreerClientAuthentifieAsync();

            var reponse = await client.PostAsJsonAsync("/api/auth/register", new
            {
                Identifiant = "marie.tremblay",
                Courriel = "marie.tremblay@geotrack.test",
                MotDePasse = "MotDePasse2"
            });

            Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);

            var corps = await reponse.Content.ReadFromJsonAsync<ReponseInscription>();
            Assert.Equal("marie.tremblay", corps!.NomComplet);
        }
    }

    // =======================================================================
    // POST /api/auth/register : validation
    // =======================================================================
    public class GEO18_InscriptionValidationTests
    {
        [Theory]
        [InlineData("Court1", "trop court (moins de 8 caracteres)")]
        [InlineData("motdepasse1", "sans majuscule")]
        [InlineData("MotDePasse", "sans chiffre")]
        public async Task Register_MotDePasseNonConforme_Retourne400(
            string motDePasse, string motif)
        {
            await using var fabrique = new FabriqueApiTest();
            var client = await fabrique.CreerClientAuthentifieAsync();

            var reponse = await client.PostAsJsonAsync("/api/auth/register", new
            {
                Identifiant = "marie.tremblay",
                Courriel = "marie.tremblay@geotrack.test",
                MotDePasse = motDePasse,
                NomComplet = "Marie Tremblay"
            });

            Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);

            var corps = await reponse.Content.ReadFromJsonAsync<ReponseErreurInscription>();
            Assert.Contains("mot de passe", corps!.Message, StringComparison.OrdinalIgnoreCase);
            Assert.NotEmpty(corps.Erreurs);

            // Motif verifie : {motif}. Aucun compte ne doit subsister.
            Assert.Null(await fabrique.TrouverUtilisateurAsync("marie.tremblay"));
            Assert.False(string.IsNullOrEmpty(motif));
        }

        [Fact]
        public async Task Register_MotDePasseConforme_EstAccepte()
        {
            await using var fabrique = new FabriqueApiTest();
            var client = await fabrique.CreerClientAuthentifieAsync();

            // 8 caracteres exactement, une majuscule, un chiffre : la limite basse.
            var reponse = await client.PostAsJsonAsync("/api/auth/register", new
            {
                Identifiant = "marie.tremblay",
                Courriel = "marie.tremblay@geotrack.test",
                MotDePasse = "Abcdefg1",
                NomComplet = "Marie Tremblay"
            });

            Assert.Equal(HttpStatusCode.Created, reponse.StatusCode);
        }

        [Fact]
        public async Task Register_IdentifiantDejaExistant_Retourne409()
        {
            await using var fabrique = new FabriqueApiTest();
            var client = await fabrique.CreerClientAuthentifieAsync();

            var reponse = await client.PostAsJsonAsync("/api/auth/register", new
            {
                // Deja pris par le compte de test authentifie.
                Identifiant = FabriqueApiTest.Identifiant,
                Courriel = "autre.courriel@geotrack.test",
                MotDePasse = "MotDePasse2",
                NomComplet = "Homonyme"
            });

            Assert.Equal(HttpStatusCode.Conflict, reponse.StatusCode);

            var corps = await reponse.Content.ReadFromJsonAsync<ReponseErreurInscription>();
            Assert.Contains("deja utilise", corps!.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(FabriqueApiTest.Identifiant, corps.Message);

            // Le compte d'origine n'a pas ete altere.
            var original = await fabrique.TrouverUtilisateurAsync(FabriqueApiTest.Identifiant);
            Assert.Equal(FabriqueApiTest.Courriel, original!.Email);
        }

        [Fact]
        public async Task Register_CourrielDejaExistant_Retourne409()
        {
            await using var fabrique = new FabriqueApiTest();
            var client = await fabrique.CreerClientAuthentifieAsync();

            var reponse = await client.PostAsJsonAsync("/api/auth/register", new
            {
                Identifiant = "identifiant.libre",
                Courriel = FabriqueApiTest.Courriel,
                MotDePasse = "MotDePasse2",
                NomComplet = "Doublon Courriel"
            });

            Assert.Equal(HttpStatusCode.Conflict, reponse.StatusCode);

            var corps = await reponse.Content.ReadFromJsonAsync<ReponseErreurInscription>();
            Assert.Contains("courriel", corps!.Message, StringComparison.OrdinalIgnoreCase);

            Assert.Null(await fabrique.TrouverUtilisateurAsync("identifiant.libre"));
        }

        [Theory]
        [InlineData("", "marie@geotrack.test", "MotDePasse2", "Identifiant")]
        [InlineData("marie", "", "MotDePasse2", "Courriel")]
        [InlineData("marie", "marie@geotrack.test", "", "MotDePasse")]
        public async Task Register_ChampObligatoireManquant_Retourne400(
            string identifiant, string courriel, string motDePasse, string champAttendu)
        {
            await using var fabrique = new FabriqueApiTest();
            var client = await fabrique.CreerClientAuthentifieAsync();

            var reponse = await client.PostAsJsonAsync("/api/auth/register", new
            {
                Identifiant = identifiant,
                Courriel = courriel,
                MotDePasse = motDePasse,
                NomComplet = "Marie Tremblay"
            });

            Assert.Equal(HttpStatusCode.BadRequest, reponse.StatusCode);

            var corps = await reponse.Content.ReadFromJsonAsync<ReponseErreurInscription>();
            Assert.Contains(corps!.Erreurs, erreur => erreur.Contains(champAttendu));
        }
    }

    // =======================================================================
    // Semeur de developpement
    // =======================================================================
    public class GEO18_SemeurDeveloppementTests
    {
        private static OptionsSeedDeveloppement OptionsParDefaut() => new();

        [Fact]
        public async Task Semeur_NeSexecutePas_HorsEnvironnementDevelopment()
        {
            // La fabrique demarre l'API en environnement "Testing" : la garde
            // de SemerSiDeveloppementAsync doit avoir empeche tout semis.
            await using var fabrique = new FabriqueApiTest();

            var options = OptionsParDefaut();
            Assert.Null(await fabrique.TrouverUtilisateurAsync(options.Identifiant));
        }

        [Fact]
        public async Task Semeur_CreeLeCompte_QuandIlEstAbsent()
        {
            await using var fabrique = new FabriqueApiTest();
            var options = OptionsParDefaut();

            var cree = await fabrique.AvecGestionnaireAsync(gestionnaire =>
                SemeurDeveloppement.SemerCompteAdministrateurAsync(
                    gestionnaire, options, NullLogger.Instance));

            Assert.True(cree);

            var compte = await fabrique.TrouverUtilisateurAsync(options.Identifiant);
            Assert.NotNull(compte);
            Assert.Equal(options.Courriel, compte!.Email);
            Assert.Equal(options.NomComplet, compte.NomComplet);
            Assert.True(compte.EmailConfirmed);
        }

        [Fact]
        public async Task Semeur_MotDePasseParDefaut_RespecteLaPolitique()
        {
            await using var fabrique = new FabriqueApiTest();
            var options = OptionsParDefaut();

            await fabrique.AvecGestionnaireAsync(gestionnaire =>
                SemeurDeveloppement.SemerCompteAdministrateurAsync(
                    gestionnaire, options, NullLogger.Instance));

            // Le compte seme doit reellement pouvoir se connecter.
            var client = fabrique.CreerClient();
            var reponse = await client.PostAsJsonAsync("/api/auth/login", new
            {
                Identifiant = options.Identifiant,
                MotDePasse = options.MotDePasse
            });

            Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        }

        [Fact]
        public async Task Semeur_EstIdempotent()
        {
            await using var fabrique = new FabriqueApiTest();
            var options = OptionsParDefaut();

            var premier = await fabrique.AvecGestionnaireAsync(gestionnaire =>
                SemeurDeveloppement.SemerCompteAdministrateurAsync(
                    gestionnaire, options, NullLogger.Instance));

            var second = await fabrique.AvecGestionnaireAsync(gestionnaire =>
                SemeurDeveloppement.SemerCompteAdministrateurAsync(
                    gestionnaire, options, NullLogger.Instance));

            Assert.True(premier);
            Assert.False(second, "Un second semis ne doit pas recreer le compte.");

            var comptes = await fabrique.AvecGestionnaireAsync(gestionnaire =>
                Task.FromResult(gestionnaire.Users.Count(u => u.UserName == options.Identifiant)));
            Assert.Equal(1, comptes);
        }

        [Fact]
        public async Task Semeur_SignaleLEchec_SiMotDePasseNonConforme()
        {
            await using var fabrique = new FabriqueApiTest();
            var options = new OptionsSeedDeveloppement { MotDePasse = "faible" };

            var cree = await fabrique.AvecGestionnaireAsync(gestionnaire =>
                SemeurDeveloppement.SemerCompteAdministrateurAsync(
                    gestionnaire, options, NullLogger.Instance));

            Assert.False(cree);
            Assert.Null(await fabrique.TrouverUtilisateurAsync(options.Identifiant));
        }

        [Fact]
        public async Task Semeur_CompteSeme_PeutCreerDautresComptes()
        {
            // Scenario reel : le compte de developpement est le point d'entree
            // qui rend /api/auth/register utilisable sur un poste neuf.
            await using var fabrique = new FabriqueApiTest();
            var options = OptionsParDefaut();

            await fabrique.AvecGestionnaireAsync(gestionnaire =>
                SemeurDeveloppement.SemerCompteAdministrateurAsync(
                    gestionnaire, options, NullLogger.Instance));

            var client = fabrique.CreerClient();
            var connexion = await client.PostAsJsonAsync("/api/auth/login", new
            {
                Identifiant = options.Identifiant,
                MotDePasse = options.MotDePasse
            });
            var session = await connexion.Content.ReadFromJsonAsync<ReponseConnexion>();

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", session!.Jeton);

            var inscription = await client.PostAsJsonAsync("/api/auth/register", new
            {
                Identifiant = "collegue",
                Courriel = "collegue@geotrack.test",
                MotDePasse = "MotDePasse3",
                NomComplet = "Collegue Test"
            });

            Assert.Equal(HttpStatusCode.Created, inscription.StatusCode);
        }
    }

    // =======================================================================
    // Verification que la politique reste unique (pas de regles dupliquees)
    // =======================================================================
    public class GEO18_CoherenceReglesTests
    {
        [Fact]
        public async Task Register_AppliqueExactementLaMemePolitiqueQueIdentity()
        {
            await using var fabrique = new FabriqueApiTest();
            var client = await fabrique.CreerClientAuthentifieAsync();

            var options = fabrique.Resoudre<
                Microsoft.Extensions.Options.IOptions<IdentityOptions>>().Value;

            // Un mot de passe d'exactement RequiredLength - 1 doit etre refuse...
            var tropCourt = new string('A', options.Password.RequiredLength - 2) + "1";
            var refus = await client.PostAsJsonAsync("/api/auth/register", new
            {
                Identifiant = "trop.court",
                Courriel = "trop.court@geotrack.test",
                MotDePasse = tropCourt,
                NomComplet = "Trop Court"
            });
            Assert.Equal(HttpStatusCode.BadRequest, refus.StatusCode);

            // ...et un mot de passe pile a la longueur requise, accepte.
            var pileBon = new string('a', options.Password.RequiredLength - 2) + "A1";
            var acceptation = await client.PostAsJsonAsync("/api/auth/register", new
            {
                Identifiant = "pile.bon",
                Courriel = "pile.bon@geotrack.test",
                MotDePasse = pileBon,
                NomComplet = "Pile Bon"
            });
            Assert.Equal(HttpStatusCode.Created, acceptation.StatusCode);
        }
    }
}
