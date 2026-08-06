using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using GeoTrack.Api.Controllers;
using GeoTrack.Api.Data;
using GeoTrack.Api.Models;
using GeoTrack.Api.Models.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GeoTrack.Api.Tests
{
    /// <summary>
    /// GEO-18 : demarre l'API complete en memoire (pipeline HTTP reel) mais
    /// remplace SQL Server par une base EF InMemory dediee a chaque instance,
    /// et fournit une cle JWT de test.
    /// </summary>
    public sealed class FabriqueApiTest : WebApplicationFactory<Program>
    {
        public const string Identifiant = "jean.dubois";
        public const string Courriel = "jean.dubois@geotrack.test";

        /// <summary>Respecte la politique GEO-18 : 8+ caracteres, une majuscule, un chiffre.</summary>
        public const string MotDePasse = "MotDePasse1";

        public const string MauvaisMotDePasse = "MauvaisMdp9";

        private const string CleJwtTest =
            "cle-de-test-geo18-suffisamment-longue-pour-hmac-sha256";

        /// <summary>
        /// Program.cs utilise WebApplication.CreateBuilder : la configuration est lue
        /// AVANT que les callbacks de WebApplicationFactory ne soient appliques.
        /// Les variables d'environnement, elles, sont prises en compte des la creation
        /// du builder : c'est le seul point d'injection fiable pour Jwt:Cle.
        /// </summary>
        static FabriqueApiTest()
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
            Environment.SetEnvironmentVariable("Jwt__Cle", CleJwtTest);
            Environment.SetEnvironmentVariable("Jwt__Emetteur", "GeoTrack.Api");
            Environment.SetEnvironmentVariable("Jwt__Audience", "GeoTrack.Web");
            Environment.SetEnvironmentVariable("Jwt__DureeMinutes", "60");
        }

        private readonly string _nomBase = $"geotrack-auth-{Guid.NewGuid()}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureServices(services =>
            {
                // Retire toute la configuration EF liee a GeoTrackContext (provider SqlServer).
                var aRetirer = services
                    .Where(d =>
                        d.ServiceType == typeof(GeoTrackContext)
                        || d.ServiceType == typeof(DbContextOptions)
                        || d.ServiceType == typeof(DbContextOptions<GeoTrackContext>)
                        || (d.ServiceType.FullName?.Contains("DbContextOptionsConfiguration") ?? false))
                    .ToList();

                foreach (var descripteur in aRetirer)
                {
                    services.Remove(descripteur);
                }

                services.AddDbContext<GeoTrackContext>(options => options.UseInMemoryDatabase(_nomBase));
            });
        }

        public HttpClient CreerClient() => CreateClient(new WebApplicationFactoryClientOptions
        {
            // Un eventuel 307 de redirection HTTPS doit apparaitre comme tel,
            // plutot que de faire echouer la requete de maniere obscure.
            AllowAutoRedirect = false
        });

        /// <summary>Cree le compte de test et retourne son identifiant technique.</summary>
        public async Task<string> SemerUtilisateurAsync()
        {
            using var portee = Services.CreateScope();
            var gestionnaire = portee.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var utilisateur = new ApplicationUser
            {
                UserName = Identifiant,
                Email = Courriel,
                EmailConfirmed = true,
                NomComplet = "Jean Dubois"
            };

            var resultat = await gestionnaire.CreateAsync(utilisateur, MotDePasse);
            Assert.True(
                resultat.Succeeded,
                "Creation du compte de test echouee : "
                    + string.Join(", ", resultat.Errors.Select(e => e.Code)));

            return utilisateur.Id;
        }

        /// <summary>Relit l'utilisateur depuis une portee neuve (etat de verrouillage a jour).</summary>
        public async Task<(ApplicationUser Utilisateur, bool EstVerrouille, int Echecs)> RelireUtilisateurAsync()
        {
            using var portee = Services.CreateScope();
            var gestionnaire = portee.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var utilisateur = await gestionnaire.FindByNameAsync(Identifiant);
            Assert.NotNull(utilisateur);

            return (utilisateur!,
                await gestionnaire.IsLockedOutAsync(utilisateur!),
                await gestionnaire.GetAccessFailedCountAsync(utilisateur!));
        }

        public T Resoudre<T>() where T : notnull
        {
            using var portee = Services.CreateScope();
            return portee.ServiceProvider.GetRequiredService<T>();
        }

        /// <summary>
        /// Sieme le compte de test, se connecte et retourne un client dont
        /// l'en-tete Authorization porte deja un jeton valide.
        /// </summary>
        public async Task<HttpClient> CreerClientAuthentifieAsync()
        {
            await SemerUtilisateurAsync();
            var client = CreerClient();

            var reponse = await client.PostAsJsonAsync("/api/auth/login", new
            {
                Identifiant = Identifiant,
                MotDePasse = MotDePasse
            });

            Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
            var corps = await reponse.Content.ReadFromJsonAsync<ReponseConnexion>();

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", corps!.Jeton);

            return client;
        }

        /// <summary>Retrouve un compte par son nom d'utilisateur, ou null.</summary>
        public async Task<ApplicationUser?> TrouverUtilisateurAsync(string identifiant)
        {
            using var portee = Services.CreateScope();
            var gestionnaire = portee.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            return await gestionnaire.FindByNameAsync(identifiant);
        }

        /// <summary>Execute une action avec un UserManager pris dans une portee neuve.</summary>
        public async Task<T> AvecGestionnaireAsync<T>(
            Func<UserManager<ApplicationUser>, Task<T>> action)
        {
            using var portee = Services.CreateScope();
            var gestionnaire = portee.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            return await action(gestionnaire);
        }
    }

    // =======================================================================
    // Protection d'acces : /api/positionsgps exige un jeton
    // =======================================================================
    public class GEO18_ProtectionAccesTests
    {
        [Fact]
        public async Task Get_PositionsGps_SansJeton_Retourne401()
        {
            await using var fabrique = new FabriqueApiTest();
            var client = fabrique.CreerClient();

            var reponse = await client.GetAsync("/api/positionsgps");

            Assert.Equal(HttpStatusCode.Unauthorized, reponse.StatusCode);
        }

        [Fact]
        public async Task Post_PositionsGps_SansJeton_Retourne401()
        {
            await using var fabrique = new FabriqueApiTest();
            var client = fabrique.CreerClient();

            var position = new PositionGps
            {
                VehiculeId = "VEH-001",
                Latitude = 45.5017,
                Longitude = -73.5673,
                Vitesse = 62.5,
                Direction = 180,
                Horodatage = new DateTime(2026, 8, 6, 12, 0, 0, DateTimeKind.Utc),
                EtatVehicule = "en_route"
            };

            var reponse = await client.PostAsJsonAsync("/api/positionsgps", position);

            Assert.Equal(HttpStatusCode.Unauthorized, reponse.StatusCode);
        }

        [Fact]
        public async Task Get_PositionsGps_AvecJetonInvalide_Retourne401()
        {
            await using var fabrique = new FabriqueApiTest();
            var client = fabrique.CreerClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", "ceci.nest.pas.un.jeton");

            var reponse = await client.GetAsync("/api/positionsgps");

            Assert.Equal(HttpStatusCode.Unauthorized, reponse.StatusCode);
        }

        [Fact]
        public async Task Get_PositionsGps_AvecJetonValide_Retourne200()
        {
            await using var fabrique = new FabriqueApiTest();
            await fabrique.SemerUtilisateurAsync();
            var client = fabrique.CreerClient();

            var connexion = await client.PostAsJsonAsync("/api/auth/login", new
            {
                Identifiant = FabriqueApiTest.Identifiant,
                MotDePasse = FabriqueApiTest.MotDePasse
            });
            var jeton = (await connexion.Content.ReadFromJsonAsync<ReponseConnexion>())!.Jeton;

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jeton);
            var reponse = await client.GetAsync("/api/positionsgps");

            Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        }

        [Fact]
        public void PositionsGpsController_PorteLAttributAuthorize()
        {
            // Garde-fou : empeche qu'un futur remaniement retire silencieusement la protection.
            var attribut = typeof(PositionsGpsController)
                .GetCustomAttribute<AuthorizeAttribute>(inherit: true);

            Assert.NotNull(attribut);
        }
    }

    // =======================================================================
    // Connexion : POST /api/auth/login
    // =======================================================================
    public class GEO18_ConnexionTests
    {
        [Fact]
        public async Task Login_AvecIdentifiantsValides_RenvoieUnJeton()
        {
            await using var fabrique = new FabriqueApiTest();
            await fabrique.SemerUtilisateurAsync();
            var client = fabrique.CreerClient();

            var reponse = await client.PostAsJsonAsync("/api/auth/login", new
            {
                Identifiant = FabriqueApiTest.Identifiant,
                MotDePasse = FabriqueApiTest.MotDePasse
            });

            Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);

            var corps = await reponse.Content.ReadFromJsonAsync<ReponseConnexion>();
            Assert.NotNull(corps);
            Assert.False(string.IsNullOrWhiteSpace(corps!.Jeton));
            Assert.Equal(FabriqueApiTest.Identifiant, corps.Identifiant);

            // Un JWT est bien forme de trois segments separes par des points.
            Assert.Equal(3, corps.Jeton.Split('.').Length);
        }

        [Fact]
        public async Task Login_AvecLeCourriel_FonctionneAussi()
        {
            await using var fabrique = new FabriqueApiTest();
            await fabrique.SemerUtilisateurAsync();
            var client = fabrique.CreerClient();

            var reponse = await client.PostAsJsonAsync("/api/auth/login", new
            {
                Identifiant = FabriqueApiTest.Courriel,
                MotDePasse = FabriqueApiTest.MotDePasse
            });

            Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        }

        [Fact]
        public async Task Login_JetonValide60Minutes()
        {
            await using var fabrique = new FabriqueApiTest();
            await fabrique.SemerUtilisateurAsync();
            var client = fabrique.CreerClient();

            var reponse = await client.PostAsJsonAsync("/api/auth/login", new
            {
                Identifiant = FabriqueApiTest.Identifiant,
                MotDePasse = FabriqueApiTest.MotDePasse
            });

            var corps = (await reponse.Content.ReadFromJsonAsync<ReponseConnexion>())!;
            var jeton = new JwtSecurityTokenHandler().ReadJwtToken(corps.Jeton);

            var duree = jeton.ValidTo - jeton.ValidFrom;
            Assert.InRange(duree.TotalMinutes, 59.5, 60.5);

            // L'expiration annoncee au client correspond a celle inscrite dans le jeton.
            Assert.InRange((corps.Expiration - jeton.ValidTo).TotalSeconds, -2, 2);
        }

        [Fact]
        public async Task Login_AvecMotDePasseErrone_Retourne401EtMessageGenerique()
        {
            await using var fabrique = new FabriqueApiTest();
            await fabrique.SemerUtilisateurAsync();
            var client = fabrique.CreerClient();

            var reponse = await client.PostAsJsonAsync("/api/auth/login", new
            {
                Identifiant = FabriqueApiTest.Identifiant,
                MotDePasse = FabriqueApiTest.MauvaisMotDePasse
            });

            Assert.Equal(HttpStatusCode.Unauthorized, reponse.StatusCode);

            var corps = await reponse.Content.ReadFromJsonAsync<ReponseErreurAuth>();
            Assert.Equal(AuthController.MessageEchecGenerique, corps!.Message);
            Assert.Equal("Identifiant ou mot de passe incorrect", corps.Message);
        }

        [Fact]
        public async Task Login_AvecIdentifiantInconnu_Retourne401EtMessageGenerique()
        {
            await using var fabrique = new FabriqueApiTest();
            await fabrique.SemerUtilisateurAsync();
            var client = fabrique.CreerClient();

            var reponse = await client.PostAsJsonAsync("/api/auth/login", new
            {
                Identifiant = "inconnu.au.bataillon",
                MotDePasse = FabriqueApiTest.MotDePasse
            });

            Assert.Equal(HttpStatusCode.Unauthorized, reponse.StatusCode);

            var corps = await reponse.Content.ReadFromJsonAsync<ReponseErreurAuth>();
            Assert.Equal(AuthController.MessageEchecGenerique, corps!.Message);
        }

        [Fact]
        public async Task Login_NeRevelePasLequelDesDeuxChampsEstErrone()
        {
            await using var fabrique = new FabriqueApiTest();
            await fabrique.SemerUtilisateurAsync();
            var client = fabrique.CreerClient();

            var compteInconnu = await client.PostAsJsonAsync("/api/auth/login", new
            {
                Identifiant = "inconnu.au.bataillon",
                MotDePasse = FabriqueApiTest.MotDePasse
            });

            var motDePasseErrone = await client.PostAsJsonAsync("/api/auth/login", new
            {
                Identifiant = FabriqueApiTest.Identifiant,
                MotDePasse = FabriqueApiTest.MauvaisMotDePasse
            });

            // Meme statut ET meme corps : impossible de distinguer les deux cas.
            Assert.Equal(compteInconnu.StatusCode, motDePasseErrone.StatusCode);
            Assert.Equal(
                await compteInconnu.Content.ReadAsStringAsync(),
                await motDePasseErrone.Content.ReadAsStringAsync());

            var texte = await motDePasseErrone.Content.ReadAsStringAsync();
            Assert.DoesNotContain("utilisateur introuvable", texte, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("verrouill", texte, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(FabriqueApiTest.Identifiant, texte, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Login_SansCorpsUtilisable_Retourne401Generique()
        {
            await using var fabrique = new FabriqueApiTest();
            await fabrique.SemerUtilisateurAsync();
            var client = fabrique.CreerClient();

            var reponse = await client.PostAsJsonAsync("/api/auth/login", new
            {
                Identifiant = "",
                MotDePasse = ""
            });

            Assert.Equal(HttpStatusCode.Unauthorized, reponse.StatusCode);
            var corps = await reponse.Content.ReadFromJsonAsync<ReponseErreurAuth>();
            Assert.Equal(AuthController.MessageEchecGenerique, corps!.Message);
        }
    }

    // =======================================================================
    // Verrouillage de compte
    // =======================================================================
    public class GEO18_VerrouillageTests
    {
        private static async Task<HttpResponseMessage> TenterConnexionEchouee(
            HttpClient client) =>
            await client.PostAsJsonAsync("/api/auth/login", new
            {
                Identifiant = FabriqueApiTest.Identifiant,
                MotDePasse = FabriqueApiTest.MauvaisMotDePasse
            });

        [Fact]
        public async Task Compte_EstVerrouilleApres5TentativesEchouees()
        {
            await using var fabrique = new FabriqueApiTest();
            await fabrique.SemerUtilisateurAsync();
            var client = fabrique.CreerClient();

            for (var tentative = 1; tentative <= 5; tentative++)
            {
                var reponse = await TenterConnexionEchouee(client);
                Assert.Equal(HttpStatusCode.Unauthorized, reponse.StatusCode);
            }

            var (_, estVerrouille, _) = await fabrique.RelireUtilisateurAsync();
            Assert.True(estVerrouille, "Le compte aurait du etre verrouille apres 5 echecs.");
        }

        [Fact]
        public async Task Compte_NestPasVerrouilleApres4TentativesEchouees()
        {
            await using var fabrique = new FabriqueApiTest();
            await fabrique.SemerUtilisateurAsync();
            var client = fabrique.CreerClient();

            for (var tentative = 1; tentative <= 4; tentative++)
            {
                await TenterConnexionEchouee(client);
            }

            var (_, estVerrouille, echecs) = await fabrique.RelireUtilisateurAsync();
            Assert.False(estVerrouille, "Le verrouillage ne doit pas se declencher avant 5 echecs.");
            Assert.Equal(4, echecs);
        }

        [Fact]
        public async Task CompteVerrouille_RefuseMemeLeBonMotDePasse()
        {
            await using var fabrique = new FabriqueApiTest();
            await fabrique.SemerUtilisateurAsync();
            var client = fabrique.CreerClient();

            for (var tentative = 1; tentative <= 5; tentative++)
            {
                await TenterConnexionEchouee(client);
            }

            var reponse = await client.PostAsJsonAsync("/api/auth/login", new
            {
                Identifiant = FabriqueApiTest.Identifiant,
                MotDePasse = FabriqueApiTest.MotDePasse
            });

            Assert.Equal(HttpStatusCode.Unauthorized, reponse.StatusCode);

            // Le verrouillage ne doit pas transparaitre dans la reponse.
            var corps = await reponse.Content.ReadFromJsonAsync<ReponseErreurAuth>();
            Assert.Equal(AuthController.MessageEchecGenerique, corps!.Message);
        }

        [Fact]
        public async Task Verrouillage_DureCinqMinutes()
        {
            await using var fabrique = new FabriqueApiTest();
            await fabrique.SemerUtilisateurAsync();
            var client = fabrique.CreerClient();

            var avant = DateTimeOffset.UtcNow;
            for (var tentative = 1; tentative <= 5; tentative++)
            {
                await TenterConnexionEchouee(client);
            }

            var (utilisateur, estVerrouille, _) = await fabrique.RelireUtilisateurAsync();

            Assert.True(estVerrouille);
            Assert.NotNull(utilisateur.LockoutEnd);

            var dureeRestante = utilisateur.LockoutEnd!.Value - avant;
            Assert.InRange(dureeRestante.TotalMinutes, 4.5, 5.1);
        }

        [Fact]
        public async Task ConnexionReussie_RemetLeCompteurDEchecsAZero()
        {
            await using var fabrique = new FabriqueApiTest();
            await fabrique.SemerUtilisateurAsync();
            var client = fabrique.CreerClient();

            await TenterConnexionEchouee(client);
            await TenterConnexionEchouee(client);

            var reponse = await client.PostAsJsonAsync("/api/auth/login", new
            {
                Identifiant = FabriqueApiTest.Identifiant,
                MotDePasse = FabriqueApiTest.MotDePasse
            });
            Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);

            var (_, estVerrouille, echecs) = await fabrique.RelireUtilisateurAsync();
            Assert.False(estVerrouille);
            Assert.Equal(0, echecs);
        }
    }

    // =======================================================================
    // Configuration d'Identity (regles GEO-18)
    // =======================================================================
    public class GEO18_ConfigurationIdentityTests
    {
        [Fact]
        public async Task Options_RespectentLesReglesDemandees()
        {
            await using var fabrique = new FabriqueApiTest();
            var options = fabrique.Resoudre<IOptions<IdentityOptions>>().Value;

            Assert.Equal(8, options.Password.RequiredLength);
            Assert.True(options.Password.RequireUppercase);
            Assert.True(options.Password.RequireDigit);

            Assert.Equal(5, options.Lockout.MaxFailedAccessAttempts);
            Assert.Equal(TimeSpan.FromMinutes(5), options.Lockout.DefaultLockoutTimeSpan);
            Assert.True(options.Lockout.AllowedForNewUsers);
        }

        [Theory]
        [InlineData("Court1", "PasswordTooShort")]
        [InlineData("motdepasse1", "PasswordRequiresUpper")]
        [InlineData("MotDePasse", "PasswordRequiresDigit")]
        public async Task MotDePasse_NonConforme_EstRefuse(string motDePasse, string codeAttendu)
        {
            await using var fabrique = new FabriqueApiTest();
            using var portee = fabrique.Services.CreateScope();
            var gestionnaire = portee.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var utilisateur = new ApplicationUser
            {
                UserName = "candidat",
                Email = "candidat@geotrack.test"
            };

            var resultat = await gestionnaire.CreateAsync(utilisateur, motDePasse);

            Assert.False(resultat.Succeeded);
            Assert.Contains(resultat.Errors, e => e.Code == codeAttendu);
        }

        [Fact]
        public async Task MotDePasse_Conforme_EstAccepte()
        {
            await using var fabrique = new FabriqueApiTest();
            using var portee = fabrique.Services.CreateScope();
            var gestionnaire = portee.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var utilisateur = new ApplicationUser
            {
                UserName = "conforme",
                Email = "conforme@geotrack.test"
            };

            var resultat = await gestionnaire.CreateAsync(utilisateur, "MotDePasse1");

            Assert.True(resultat.Succeeded);
        }

        [Fact]
        public async Task Contexte_ExposeLesTablesIdentity()
        {
            await using var fabrique = new FabriqueApiTest();
            using var portee = fabrique.Services.CreateScope();
            var contexte = portee.ServiceProvider.GetRequiredService<GeoTrackContext>();

            // GeoTrackContext herite bien d'IdentityDbContext<ApplicationUser>.
            Assert.NotNull(contexte.Users);
            Assert.NotNull(contexte.Roles);
            Assert.NotNull(contexte.PositionsGps);
        }
    }
}
