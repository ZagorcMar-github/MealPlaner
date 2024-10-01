using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MealPlaner.authentication
{
    public class APIKeyAuthFilter : IAsyncAuthorizationFilter
    {
        private readonly IConfiguration _configuration;
        public APIKeyAuthFilter(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {  

        
            if (!context.HttpContext.Request.Headers.TryGetValue(AuthConstants.ApiKeyheaderName, out
                    var extractedApiKey))
            {
                context.Result= new UnauthorizedObjectResult("api Key missing");
                return;
            };
            var apiKey = _configuration.GetValue<string>(AuthConstants.ApiKeySectionName);
            if (!apiKey.Equals(extractedApiKey))
            {
                context.Result = new UnauthorizedObjectResult("invalid api key");
                return;
            }
            
            
    }
    }
}
