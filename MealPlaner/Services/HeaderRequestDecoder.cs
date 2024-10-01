using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace MealPlaner.Services
{
    public class HeaderRequestDecoder
    {
        public static string? ExtractUserIdFromJwt(HttpContext httpContext)
        {
            var authorizationHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault();

            if (!string.IsNullOrEmpty(authorizationHeader) && authorizationHeader.StartsWith("Bearer "))
            {
                var token = authorizationHeader.Substring("Bearer ".Length).Trim();

                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.ReadJwtToken(token);

                
                var subtier = jwtToken.Claims.FirstOrDefault(c => c.Type == "Subscription")?.Value;
                var userId= jwtToken.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
                Console.WriteLine(subtier);
                return subtier;
            }

            return null;
        }
    }
}
