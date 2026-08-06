using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GeoTrack.Api.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GeoTrack.Api.Services
{
    public interface IJetonJwtService
    {
        (string Jeton, DateTime Expiration) Generer(ApplicationUser utilisateur);
    }

    /// <summary>
    /// GEO-18 : fabrique les jetons JWT signes en HMAC-SHA256.
    /// Isole de l'AuthController pour rester testable sans pipeline HTTP.
    /// </summary>
    public class JetonJwtService : IJetonJwtService
    {
        private readonly OptionsJwt _options;

        public JetonJwtService(IOptions<OptionsJwt> options)
        {
            _options = options.Value;
        }

        public (string Jeton, DateTime Expiration) Generer(ApplicationUser utilisateur)
        {
            var expiration = DateTime.UtcNow.AddMinutes(_options.DureeMinutes);

            var revendications = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, utilisateur.Id),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(ClaimTypes.NameIdentifier, utilisateur.Id),
                new(ClaimTypes.Name, utilisateur.UserName ?? string.Empty)
            };

            if (!string.IsNullOrWhiteSpace(utilisateur.Email))
            {
                revendications.Add(new Claim(JwtRegisteredClaimNames.Email, utilisateur.Email));
            }

            var cle = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Cle));
            var identifiants = new SigningCredentials(cle, SecurityAlgorithms.HmacSha256);

            var jeton = new JwtSecurityToken(
                issuer: _options.Emetteur,
                audience: _options.Audience,
                claims: revendications,
                notBefore: DateTime.UtcNow,
                expires: expiration,
                signingCredentials: identifiants);

            return (new JwtSecurityTokenHandler().WriteToken(jeton), expiration);
        }
    }
}
