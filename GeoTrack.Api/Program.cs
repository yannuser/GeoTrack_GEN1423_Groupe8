using System.Text;
using GeoTrack.Api.Data;
using GeoTrack.Api.Models;
using GeoTrack.Api.Services;
using GeoTrack.Api.Services.HistoriqueTrajets;
using GeoTrack.Api.Services.Resilience;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddControllers();

// GEO-27 : autorise le frontend GeoTrack.Web (Vite) a appeler l'API en developpement.
const string PolitiqueCorsDeveloppement = "GeoTrackWebDev";
builder.Services.AddCors(options =>
{
    options.AddPolicy(PolitiqueCorsDeveloppement, politique =>
        politique
            .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddDbContext<GeoTrackContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("GeoTrackDb")));

// ---------------------------------------------------------------------------
// GEO-18 : ASP.NET Core Identity
// ---------------------------------------------------------------------------
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        // Mot de passe : 8 caracteres minimum, au moins une majuscule et un chiffre.
        options.Password.RequiredLength = 8;
        options.Password.RequireUppercase = true;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;

        // Verrouillage : 5 tentatives echouees, puis 5 minutes de blocage.
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.AllowedForNewUsers = true;

        options.User.RequireUniqueEmail = true;
    })
    .AddSignInManager()
    .AddEntityFrameworkStores<GeoTrackContext>()
    .AddDefaultTokenProviders();

// ---------------------------------------------------------------------------
// GEO-18 : authentification par jeton JWT (60 minutes)
// ---------------------------------------------------------------------------
builder.Services.Configure<OptionsJwt>(builder.Configuration.GetSection(OptionsJwt.Section));
builder.Services.AddScoped<IJetonJwtService, JetonJwtService>();

// ---------------------------------------------------------------------------
// Branchement des services metier
// ---------------------------------------------------------------------------

// GEO-12 : historique des trajets. Injecte dans TrajetsController.
// Scoped : le service est sans etat propre (ses donnees sont statiques),
// une instance par requete suffit.
builder.Services.AddScoped<HistoriqueTrajetService>();

// GEO-16 : resilience / haute disponibilite. Injecte dans HealthController.
// Singleton OBLIGATOIRE : l'etat des circuit breakers, les metriques et le
// journal d'evenements sont portes par l'instance. En Scoped, chaque requete
// repartirait d'un circuit vide et le disjoncteur ne se declencherait jamais.
// Les collections internes sont concurrentes, l'usage partage est sur.
builder.Services.AddSingleton<ResilienceService>();

// GEO-15 : VehiculeService n'est PAS enregistre. Il depend de
// IVehiculeRepository / IConducteurRepository / IGroupeRepository, dont aucune
// implementation n'existe dans le depot. Voir le rapport de branchement.

var optionsJwt = builder.Configuration.GetSection(OptionsJwt.Section).Get<OptionsJwt>() ?? new OptionsJwt();

if (string.IsNullOrWhiteSpace(optionsJwt.Cle) || optionsJwt.Cle.Length < 32)
{
    throw new InvalidOperationException(
        "La cle de signature JWT est absente ou trop courte (32 caracteres minimum). " +
        "Renseignez 'Jwt:Cle' via appsettings.Development.json, les user-secrets " +
        "ou la variable d'environnement Jwt__Cle.");
}

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = optionsJwt.Emetteur,
            ValidateAudience = true,
            ValidAudience = optionsJwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(optionsJwt.Cle)),
            ValidateLifetime = true,
            // Pas de tolerance : un jeton expire depuis 1 seconde est refuse.
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.UseCors(PolitiqueCorsDeveloppement);
}

// L'ordre compte : authentification avant autorisation, avant les controleurs.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// GEO-18 : compte administrateur local. La methode verifie elle-meme
// l'environnement et n'agit qu'en Development (voir SemeurDeveloppement).
await SemeurDeveloppement.SemerSiDeveloppementAsync(app);

app.Run();

/// <summary>
/// Rend la classe Program visible depuis GeoTrack.Api.Tests pour permettre
/// les tests d'integration via WebApplicationFactory&lt;Program&gt;.
/// </summary>
public partial class Program { }
