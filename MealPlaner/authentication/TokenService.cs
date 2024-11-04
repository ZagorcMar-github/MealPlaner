using MealPlaner.Identity;
using MealPlaner.Models;
using Microsoft.IdentityModel.Tokens;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace MealPlaner.authentication
{
    public class TokenService
    {
        private const string TokenSecret = "StoreElswhere123!0582E01C-2088-4549-84D1-3A4343F1BF19";
        private static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(1);

        /// <summary>
        /// Generates a JSON Web Token (JWT) for the specified user, incorporating claims for user identity and role-based access.
        /// - **Claims**: Includes the user's name and ID as claims. Adds subscription and admin status claims if applicable.
        /// - **Token Security**: Signs the token using HMAC-SHA256 for integrity and security.
        /// - **Token Lifetime**: Sets an expiration date based on `TokenLifetime`, providing time-limited access.
        /// - **Audience and Issuer**: Specifies the intended audience and issuer for added security.
        /// </summary>
        /// <param name="user">An instance of <see cref="User"/> representing the user for whom the token is being generated.</param>
        /// <returns>Returns a <see cref="string"/> containing the generated JWT token, signed and ready for client use.</returns>
        /// <exception cref="Exception">Rethrows any exceptions encountered during token generation, allowing for higher-level handling.</exception>

        public string GenerateToken(User user) {

            try
            {
                List<Claim> claims = new List<Claim> {
                new Claim("name", user.Username),
                
                new Claim("UserId",user.UserId.ToString())

            };
                if (user.Subscription != null) {
                    claims.Add(new Claim(CustomIdentityConstants.UserSubtierClaimName, user.Subscription));
                }
                if (user.Admin =="1")
                {
                    claims.Add(new Claim(CustomIdentityConstants.UserAdminClaimName, user.Admin));
                }

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TokenSecret)!);
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
                var token = new JwtSecurityToken(
                    claims: claims,
                    expires: DateTime.UtcNow.Add(TokenLifetime),
                    issuer: "someGuy",
                    audience: "mySpecialsite",
                    signingCredentials: creds
                    );
                var jwt = new JwtSecurityTokenHandler().WriteToken(token);
                Console.WriteLine(jwt);
                return jwt;
            }
            catch (Exception)
            {

                throw;
            }
           
        }


    }
}
