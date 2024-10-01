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
