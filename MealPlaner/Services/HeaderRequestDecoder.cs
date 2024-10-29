using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace MealPlaner.Services
{
    public class HeaderRequestDecoder
    {
        /// <summary>
        /// Extracts the user ID from a JWT token provided in the `Authorization` header of the HTTP request.
        /// This method expects the token to be in the format `Bearer {token}` and retrieves the `UserId` claim if available.
        /// </summary>
        /// <param name="httpContext">The <see cref="HttpContext"/> of the request containing the `Authorization` header with the JWT token.</param>
        /// <returns>Returns the user ID as a <see cref="string"/> if the `UserId` claim is present in the token; otherwise, returns null.</returns>
        /// <exception cref="SecurityTokenException">Thrown if there is an error while reading or parsing the JWT token.</exception>

        public string? ExtractUserIdFromJwt(HttpContext httpContext)
        {
            var authorizationHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault();

            if (!string.IsNullOrEmpty(authorizationHeader) && authorizationHeader.StartsWith("Bearer "))
            {
                var token = authorizationHeader.Substring("Bearer ".Length).Trim();

                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.ReadJwtToken(token);

                

                var userId= jwtToken.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
                return userId;
            }


            return null;
        }
        /// <summary>
        /// Extracts the subscription tier from a JWT token provided in the `Authorization` header of the HTTP request.
        /// This method expects the token to be in the format `Bearer {token}` and retrieves the `Subscription` claim if available.
        /// </summary>
        /// <param name="httpContext">The <see cref="HttpContext"/> of the request containing the `Authorization` header with the JWT token.</param>
        /// <returns>Returns the subscription tier as a <see cref="string"/> if the `Subscription` claim is present in the token; otherwise, returns null.</returns>
        /// <exception cref="SecurityTokenException">Thrown if there is an error while reading or parsing the JWT token.</exception>

        public static string? ExtractSubtierFromJwt(HttpContext httpContext)
        {
            var authorizationHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault();

            if (!string.IsNullOrEmpty(authorizationHeader) && authorizationHeader.StartsWith("Bearer  "))
            {
                var token = authorizationHeader.Substring("Bearer ".Length).Trim();

                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.ReadJwtToken(token);


                var subtier = jwtToken.Claims.FirstOrDefault(c => c.Type == "Subscription")?.Value;
                return subtier;
            }


            return null;
        }
    }
}
